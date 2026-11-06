import { useEffect, useRef, useState, type ReactNode } from "react";
import { useValue } from "cs2/api";
import { c } from "utils/classes";
import {
    CONSIST_PREVIEW_DROP_GAP,
    CONSIST_PREVIEW_LAYOUT,
    CONSIST_PREVIEW_URL,
    consistPreviewDrag,
    consistPreviewDragEnd,
    consistPreviewHighlight,
    consistPreviewOrbit,
    consistPreviewPan,
    consistPreviewReset,
    consistPreviewShow,
    consistPreviewZoom,
} from "../bindings";
import type { ConsistSlot } from "../vehicle-stats";
import styles from "./consist-preview.module.scss";

export interface ConsistStageProps {
    cars: ConsistSlot[];
    nameOf: (id: string) => string;

    inBlock: (i: number) => boolean;
    /** The coupling new cars go into: gap g sits in front of car g; gap cars.length is the tail. */
    insertGap: number | null;
    /** Gap a drag would drop into, drawn as a marker; null when nothing is being dragged. */
    dropGap: number | null;
    dragging: boolean;

    onCarMouseDown: (i: number, shift: boolean) => void;
    onCarClick: (i: number, shift: boolean) => void;
    onCouplingClick: (gap: number) => void;
    onHoverGap: (gap: number) => void;
    /** The pointer left the stage: a drop now would land nowhere. */
    onLeave: () => void;

    /** The selected block, tinted on the cars themselves. */
    selection: { start: number; count: number } | null;
    /** Floated on the selected cars: their figures, edited where the cars are. */
    card?: ReactNode;
    /** The block a press on car i drags: the selection when i is in it, else the car alone. */
    blockFor: (i: number) => { start: number; count: number };
    /** Moves a block so it starts at `to` (a position with the block lifted out), as consistMove does. */
    onMoveBlock: (start: number, count: number, to: number) => void;

    hint: string;
}

const pct = (v: number) => `${(v * 100).toFixed(3)}%`;

/**
 * The train editor: the built train rendered in 3D by the game, with click, drag and drop zones laid over each
 * car and coupling where the camera projects them (published by BTS_ConsistPreviewSystem). The game renders it
 * only while this is mounted.
 *
 * Left-drag on the background slides along the train, right-drag turns the view, the wheel zooms and a
 * double-click on the background puts the view back side-on.
 */
export const ConsistStage = (props: ConsistStageProps) => {
    const { cars, nameOf, inBlock, insertGap, dropGap, dragging } = props;
    const url = useValue(CONSIST_PREVIEW_URL.binding);
    const layout = useValue(CONSIST_PREVIEW_LAYOUT.binding);
    const stage = useRef<HTMLDivElement | null>(null);
    const view = useRef<{ x: number; y: number; orbit: boolean } | null>(null);
    const [hover, setHover] = useState<number | null>(null);

    // A press on a car: becomes a physical drag once the pointer moves, else stays a click (select).
    const dropGap3D = useValue(CONSIST_PREVIEW_DROP_GAP.binding);
    const latest = useRef({ props, dropGap3D });
    latest.current = { props, dropGap3D };
    const press = useRef<{ i: number; x: number; y: number; block: { start: number; count: number } | null } | null>(null);
    const suppressClick = useRef(false);
    const [carDragging, setCarDragging] = useState(false);

    // The highlight is drawn by the game on the car models, not as a box over the picture.
    const selStart = props.selection?.start ?? -1;
    const selCount = props.selection?.count ?? 0;
    useEffect(() => consistPreviewHighlight(hover ?? -1, selStart, selCount), [hover, selStart, selCount]);

    useEffect(() => {
        consistPreviewShow(true);
        const move = (e: MouseEvent) => {
            const p = press.current;
            if (p) {
                if (!p.block) {
                    if (Math.abs(e.clientX - p.x) + Math.abs(e.clientY - p.y) < 5) return;
                    p.block = latest.current.props.blockFor(p.i);
                    setCarDragging(true);
                }
                const box = stage.current?.getBoundingClientRect();
                if (box) {
                    consistPreviewDrag(
                        p.block.start,
                        p.block.count,
                        (e.clientX - box.left) / Math.max(1, box.width),
                        (e.clientY - box.top) / Math.max(1, box.height),
                    );
                }
                return;
            }

            const v = view.current;
            if (!v) return;
            const dx = e.clientX - v.x;
            const dy = e.clientY - v.y;
            view.current = { x: e.clientX, y: e.clientY, orbit: v.orbit };
            if (v.orbit) {
                consistPreviewOrbit(dx, dy);
            } else {
                const width = stage.current?.getBoundingClientRect().width ?? 1;
                consistPreviewPan(dx / Math.max(1, width));
            }
        };
        const up = () => {
            view.current = null;
            const p = press.current;
            press.current = null;
            if (!p || !p.block) return;

            // A drag ended: the car under the pointer also gets a click, which must not reselect.
            suppressClick.current = true;
            setCarDragging(false);
            const to = latest.current.dropGap3D;
            if (to >= 0 && to !== p.block.start) latest.current.props.onMoveBlock(p.block.start, p.block.count, to);
            consistPreviewDragEnd();
        };
        document.addEventListener("mousemove", move);
        document.addEventListener("mouseup", up);
        return () => {
            document.removeEventListener("mousemove", move);
            document.removeEventListener("mouseup", up);
            consistPreviewShow(false);
        };
    }, []);

    // The layout matches the train only once the game has rebuilt it; until then, draw no zones.
    const n = cars.length;
    const ready = layout.length === n * 4 + (n + 1) * 2;
    const rect = (i: number) => ({ x0: layout[i * 4], y0: layout[i * 4 + 1], x1: layout[i * 4 + 2], y1: layout[i * 4 + 3] });
    const gapAt = (g: number) => ({ x: layout[n * 4 + g * 2], y: layout[n * 4 + g * 2 + 1] });

    // While something is dragged, the drop goes into the coupling nearest the pointer.
    const nearestGap = (clientX: number, clientY: number) => {
        const box = stage.current?.getBoundingClientRect();
        if (!box || !ready) return 0;
        const x = (clientX - box.left) / box.width;
        const y = (clientY - box.top) / box.height;
        let best = 0;
        let bestDistance = Infinity;
        for (let g = 0; g <= n; g++) {
            const p = gapAt(g);
            if (p.x < 0) continue;
            const d = (p.x - x) * (p.x - x) + 0.1 * (p.y - y) * (p.y - y);
            if (d < bestDistance) {
                bestDistance = d;
                best = g;
            }
        }
        return best;
    };

    return (
        <div
            ref={stage}
            className={styles.preview}
            onMouseDown={(e) => {
                // Cars stop their own presses; one reaching here started on the background.
                view.current = { x: e.clientX, y: e.clientY, orbit: e.button === 2 };
            }}
            onMouseMove={(e) => dragging && props.onHoverGap(nearestGap(e.clientX, e.clientY))}
            onMouseLeave={props.onLeave}
            onWheel={(e) => consistPreviewZoom(e.deltaY > 0 ? 1 : -1)}
            onDoubleClick={() => consistPreviewReset()}
        >
            {/* Set inline: css-loader would try to resolve a live:// url() at build time. */}
            {url && <div className={styles.image} style={{ backgroundImage: `url(${url})` }} />}

            {ready &&
                cars.map((car, i) => {
                    const r = rect(i);
                    if (r.x1 <= r.x0) return null;
                    return (
                        <div
                            key={`car-${i}`}
                            className={c(
                                styles.car,
                                inBlock(i) ? styles.carOn : "",
                                hover === i ? styles.carHover : "",
                                car.missing ? styles.carMissing : "",
                            )}
                            style={{ left: pct(r.x0), top: pct(r.y0), width: pct(r.x1 - r.x0), height: pct(r.y1 - r.y0) }}
                            onMouseDown={(e) => {
                                e.stopPropagation();
                                if (e.button === 0 && !e.shiftKey) press.current = { i, x: e.clientX, y: e.clientY, block: null };
                            }}
                            onClick={(e) => {
                                if (suppressClick.current) {
                                    suppressClick.current = false;
                                    return;
                                }
                                props.onCarClick(i, e.shiftKey);
                            }}
                            onMouseEnter={() => setHover(i)}
                            onMouseLeave={() => setHover((h) => (h === i ? null : h))}
                        >
                            <div className={c(styles.badge, car.lead ? styles.badgeLead : "")}>
                                {`${i + 1}${car.reversed ? " ◂" : ""}`}
                            </div>
                            {hover === i && !dragging && !carDragging && (
                                <div className={styles.label}>{car.missing ? "?" : nameOf(car.id)}</div>
                            )}
                        </div>
                    );
                })}

            {ready &&
                Array.from({ length: n + 1 }, (_, g) => {
                    const p = gapAt(g);
                    if (p.x < 0) return null;
                    return (
                        <div
                            key={`gap-${g}`}
                            className={c(
                                styles.coupling,
                                carDragging ? styles.couplingHidden : "",
                                insertGap === g ? styles.couplingOn : "",
                                dropGap === g ? styles.couplingDrop : "",
                            )}
                            style={{ left: pct(p.x), top: pct(p.y) }}
                            onMouseDown={(e) => e.stopPropagation()}
                            onClick={() => props.onCouplingClick(g)}
                        />
                    );
                })}

            {/* The selection's card, over (or under, when they're high in the view) the selected cars, following
                them as the view moves. Kept inside the picture; hidden while anything is being dragged. */}
            {ready && props.card && props.selection && !carDragging && !dragging && (() => {
                const { start, count } = props.selection;
                let x0 = Infinity;
                let x1 = -Infinity;
                let y0 = Infinity;
                let y1 = -Infinity;
                for (let i = start; i < start + count && i < n; i++) {
                    const r = rect(i);
                    if (r.x1 <= r.x0) continue;
                    x0 = Math.min(x0, r.x0);
                    x1 = Math.max(x1, r.x1);
                    y0 = Math.min(y0, r.y0);
                    y1 = Math.max(y1, r.y1);
                }
                if (!isFinite(x0)) return null;

                // The card is 270rem in a ~730rem stage: centres past 19% / 81% would push it off the sides. It is
                // up to ~0.56 of the stage tall: above the cars if that fits, else below, else docked at the top.
                const centre = Math.max(0.19, Math.min(0.81, (x0 + x1) / 2));
                const place = y0 > 0.6 ? "above" : 1 - y1 > 0.6 ? "below" : "top";
                return (
                    <div
                        className={c(
                            styles.card,
                            place === "above" ? styles.cardAbove : place === "below" ? styles.cardBelow : styles.cardTop,
                        )}
                        style={
                            place === "above"
                                ? { left: pct(centre), bottom: pct(1 - y0) }
                                : place === "below"
                                  ? { left: pct(centre), top: pct(y1) }
                                  : { left: pct(centre) }
                        }
                        onMouseDown={(e) => e.stopPropagation()}
                        onWheel={(e) => e.stopPropagation()}
                        onDoubleClick={(e) => e.stopPropagation()}
                    >
                        {props.card}
                    </div>
                );
            })()}

            {/* An empty train is one coupling: dropping anywhere puts the first car in. */}
            {n === 0 && dragging && (
                <div className={c(styles.emptyDrop, dropGap === 0 ? styles.couplingDrop : "")} onMouseMove={() => props.onHoverGap(0)} />
            )}

            {!(props.card && props.selection) && <div className={styles.hint}>{props.hint}</div>}
        </div>
    );
};
