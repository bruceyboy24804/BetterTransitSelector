import { useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useValue } from "cs2/api";
import { VC, VT } from "vanilla/Components";
import { c } from "utils/classes";
import {
    entityKey,
    useAssetText,
    useConsistBuilder,
    useT,
    type ConsistBuilder,
    type ConsistCatalogueCar,
    type CarStat,
    type ConsistSlot,
    type Entity,
} from "../vehicle-stats";
import {
    consistClear,
    consistConfigDelete,
    consistConfigOpen,
    consistConfigRename,
    consistConfigSave,
    consistConfigSaveNew,
    consistCopyRunning,
    CONSIST_PREVIEW_RUNNING,
    consistPreviewReset,
    consistPreviewRun,
    consistPreviewZoom,
    consistFlip,
    consistInsert,
    consistLoadRecipe,
    consistMirror,
    consistMove,
    consistRefit,
    consistRefitStop,
    consistRemove,
    consistRepeat,
    consistSetStat,
    consistTurnAll,
} from "../bindings";
import { CountValue, LengthValue, SpeedValue } from "./stat-value";
import { ConsistStage } from "./consist-preview";
import styles from "./consist-builder.module.scss";

// Set inline, not in the SCSS: css-loader would try to bundle a game-relative url().
const CLOSE = "Media/Glyphs/Close.svg";
// The glyphs vanilla's tool-option steppers use (brush height, elevation step).
const STEP_DOWN = "Media/Glyphs/ThickStrokeArrowDown.svg";
const STEP_UP = "Media/Glyphs/ThickStrokeArrowUp.svg";
// The visual customisation section's colour reset icon.
const RESET = "Media/Game/Icons/NewUI/Reset_Button.svg";

const KINDS = ["All", "Engine", "Unit", "Carriage", "Other"];

type Translate = ReturnType<typeof useT>;

// Slots carry their own figures (read fresh each publish), so totals never depend on the catalogue.
const totalsOf = (builder: ConsistBuilder) => ({
    passengers: builder.cars.reduce((sum, car) => sum + car.figures.passengers, 0),
    cargo: builder.cars.reduce((sum, car) => sum + car.figures.cargo, 0),
    length: builder.cars.reduce((sum, car) => sum + car.length, 0),
    // The game runs a train at its slowest car's top speed (TrainNavigationSystem.UpdateTrainLimits).
    topSpeed: builder.cars.reduce((low, car) => Math.min(low, car.figures.maxSpeed || Infinity), Infinity),
});

const useCatalogueLookup = (builder: ConsistBuilder) => {
    const byEntity = useMemo(() => {
        const map = new Map<string, ConsistCatalogueCar>();
        for (const car of builder.catalogue) map.set(entityKey(car.entity), car);
        return map;
    }, [builder.catalogue]);
    return (entity: Entity) => byEntity.get(entityKey(entity));
};

const Summary = ({ builder, t }: { builder: ConsistBuilder; t: Translate }) => {
    if (builder.cars.length === 0) {
        return <div className={styles.summary}>{t("Consist.NewTrain", "New train")}</div>;
    }

    const { passengers, cargo, length, topSpeed } = totalsOf(builder);
    return (
        <div className={styles.summary}>
            <CountValue value={builder.cars.length} />
            {` ${t("Consist.Cars", "cars")} · `}
            {cargo > 0 && passengers === 0 ? (
                <>
                    <CountValue value={cargo} />
                    {` ${t("Consist.Cargo", "cargo")}`}
                </>
            ) : (
                <>
                    <CountValue value={passengers} />
                    {` ${t("Consist.Passengers", "passengers")}`}
                </>
            )}
            {" · "}
            <LengthValue metres={Math.round(length)} />
            {isFinite(topSpeed) && (
                <>
                    {" · "}
                    <SpeedValue kph={Math.round(topSpeed)} />
                </>
            )}
        </div>
    );
};

/**
 * One row in the line's vehicle section: what the line runs, and the button that opens the builder window.
 * The window is portalled to the document body so it floats free of the info panel.
 */
export const ConsistBuilderRow = () => {
    const builder = useConsistBuilder();
    const t = useT();
    const [open, setOpen] = useState(false);

    // The window follows the selection: selecting something that is not a rail line closes it.
    useEffect(() => {
        if (!builder.valid) setOpen(false);
    }, [builder.valid]);

    if (!builder.valid) return null;

    return (
        <>
            <div className={styles.row}>
                <div className={styles.rowTitle}>{t("Consist.Title", "Train builder")}</div>
                <div className={styles.spacer} />
                <VC.ToolButton
                    src=""
                    selected={open}
                    className={c(VT.toolButton.button, styles.textButton, open ? styles.on : "")}
                    onSelect={() => setOpen(!open)}
                >
                    {open ? t("Consist.Close", "Close") : t("Consist.Open", "Open")}
                </VC.ToolButton>
            </div>

            {builder.lineState !== "Default" && <RefitRow builder={builder} t={t} />}

            {open && createPortal(<ConsistBuilderWindow onClose={() => setOpen(false)} />, document.body)}
        </>
    );
};

/** The line's running trains against the train it has picked. About the line, so it lives outside the window. */
const RefitRow = ({ builder, t }: { builder: ConsistBuilder; t: Translate }) => {
    const button = (label: string, tooltip: string, onSelect: () => void) => (
        <VC.ToolButton src="" tooltip={tooltip} className={c(VT.toolButton.button, styles.textButton)} onSelect={onSelect}>
            {label}
        </VC.ToolButton>
    );
    return (
        <div className={styles.refit}>
            <div className={styles.refitText}>
                {builder.refitting
                    ? t("Consist.Refitting", "Refitting: {COUNT} train(s) still to replace, one at a time.", { COUNT: builder.mismatched })
                    : builder.mismatched > 0
                      ? t("Consist.Mismatched", "{COUNT} running train(s) still have the old cars. New trains are built like this.", { COUNT: builder.mismatched })
                      : t("Consist.AllMatch", "Every running train matches this train.")}
            </div>
            {builder.refitting
                ? button(t("Consist.RefitStop", "Stop"), t("Consist.RefitStop.Tooltip", "Stop replacing trains. A train already leaving the line still leaves."), () => consistRefitStop())
                : builder.mismatched > 0 &&
                  button(t("Consist.Refit", "Refit running trains"), t("Consist.Refit.Tooltip", "Retire the old trains one at a time: each finishes at a stop and returns to its depot, and the depot sends a new one built like this. The line is never more than one train short."), () => consistRefit())}
        </div>
    );
};

const STAT_STEPS: Record<CarStat, number> = { passengers: 10, cargo: 5, maxSpeed: 10, acceleration: 0.1, braking: 0.1 };

/**
 * The selected cars' figures, floated on the selected cars in the 3D view: small round steps, the value between
 * them, and one Reset for every figure (edited values show in the accent colour). Shown are the first car's;
 * a step sets every selected car, switching each to a custom car (a copy of its asset with those figures); Reset
 * puts them back to the asset's. Capacity rows appear only for cars that carry that load.
 */
const CarCard = ({
    cars,
    t,
    onSet,
    onRemove,
}: {
    cars: ConsistSlot[];
    t: Translate;
    onSet: (stat: CarStat, value: number) => void;
    onRemove: () => void;
}) => {
    const first = cars[0];
    if (!first) return null;

    const stats: CarStat[] = [];
    if (first.baseFigures.passengers > 0) stats.push("passengers");
    if (first.baseFigures.cargo > 0) stats.push("cargo");
    stats.push("maxSpeed", "acceleration", "braking");

    const label: Record<CarStat, string> = {
        passengers: t("Consist.Stat.Passengers", "Passengers"),
        cargo: t("Consist.Stat.Cargo", "Cargo"),
        maxSpeed: t("Consist.Stat.MaxSpeed", "Top speed"),
        acceleration: t("Consist.Stat.Acceleration", "Acceleration"),
        braking: t("Consist.Stat.Braking", "Braking"),
    };
    const show = (stat: CarStat, v: number) =>
        stat === "maxSpeed" ? <SpeedValue kph={Math.round(v)} /> : stat === "acceleration" || stat === "braking" ? `${v.toFixed(1)} m/s²` : <CountValue value={Math.round(v)} />;

    return (
        <div className={styles.card}>
            {stats.map((stat) => {
                const value = first.figures[stat];
                const edited = cars.some((car) => car.figures[stat] !== car.baseFigures[stat]);
                const step = STAT_STEPS[stat];
                const round = (v: number) => Math.round(v / step) * step;
                return (
                    // Vanilla's tool-option stepper (the brush height one): arrow ToolButtons either side of a number
                    // field, with its classes and its increase/decrease sounds.
                    <div key={stat} className={styles.cardRow}>
                        <div className={styles.cardLabel}>{label[stat]}</div>
                        <div className={c(VT.mouseToolOptions.content, styles.cardStepper)}>
                            <VC.ToolButton
                                src={STEP_DOWN}
                                disabled={value - step <= 0}
                                selectSound="decrease-elevation"
                                className={c(VT.toolButton.button, VT.mouseToolOptions.startButton, styles.cardStep)}
                                onSelect={() => onSet(stat, round(value - step))}
                            />
                            <div className={c(VT.mouseToolOptions.numberField, styles.cardValue, edited ? styles.statEdited : "")}>
                                {show(stat, value)}
                            </div>
                            <VC.ToolButton
                                src={STEP_UP}
                                selectSound="increase-elevation"
                                className={c(VT.toolButton.button, VT.mouseToolOptions.endButton, styles.cardStep)}
                                onSelect={() => onSet(stat, round(value + step))}
                            />
                        </div>
                        {/* Vanilla's reset button, as in the visual customisation section's colour reset: the same
                            icon and action-button look, greyed out while this figure is the car's own. */}
                        <VC.IconButton
                            tinted={true}
                            src={RESET}
                            disabled={!edited}
                            tooltip={t("Consist.Stat.Default.Tooltip", "Go back to the car's own value")}
                            theme={VT.visualCustomize}
                            className={c(VT.visualCustomize.actionButton, styles.cardReset)}
                            onSelect={() => onSet(stat, 0)}
                        />
                    </div>
                );
            })}
            <div className={styles.cardFooter}>
                <VC.ToolButton src="" className={c(VT.toolButton.button, styles.textButton)} onSelect={onRemove}>
                    {t("Consist.Remove", "Remove")}
                </VC.ToolButton>
            </div>
        </div>
    );
};

/**
 * A single-choice filter drawn with vanilla's dropdown styling (a real &lt;select&gt; crashes the game UI).
 * The menu scrolls, since a big subscription list has hundreds of packs.
 */
const PickList = ({
    label,
    anyLabel,
    value,
    options,
    onChange,
    open,
    onOpen,
}: {
    label: string;
    anyLabel: string;
    value: string | null;
    options: string[];
    onChange: (value: string | null) => void;
    // Open state lives with the parent, so opening one pick list closes the other.
    open: boolean;
    onOpen: (open: boolean) => void;
}) => {
    const setOpen = onOpen;
    const choose = (next: string | null) => {
        onChange(next);
        setOpen(false);
    };

    return (
        <div className={styles.pick}>
            <div className={styles.pickLabel}>{label}</div>
            <div className={styles.pickBody}>
                {/* Vanilla's dropdown markup, not its DropdownToggle component: the component registers a focus
                    key, and two of them in this window (portalled outside the panel's focus scope) collide in
                    one MultiChildFocusController and throw. */}
                <div className={c(VT.gameDropdown.dropdownToggle, styles.pickToggle)} onClick={() => setOpen(!open)}>
                    <div className={styles.pickValue}>{value ?? anyLabel}</div>
                    <div className={VT.gameDropdown.indicator} />
                </div>
                {open && (
                    <div className={c(VT.gameDropdown.dropdownMenu, styles.pickMenu)}>
                        <VC.Scrollable vertical={true} className={styles.pickScroll}>
                            {[null, ...options].map((option) => (
                                <div
                                    key={option ?? ""}
                                    className={c(VT.gameDropdown.dropdownItem, styles.pickItem, option === value ? styles.pickItemOn : "")}
                                    onClick={() => choose(option)}
                                >
                                    {option ?? anyLabel}
                                </div>
                            ))}
                        </VC.Scrollable>
                    </div>
                )}
            </div>
        </div>
    );
};

/** Where the window opens, in screen pixels. Kept for the session so reopening puts it back where it was. */
let lastPosition = { left: 480, top: 140 };
const ConsistBuilderWindow = ({ onClose }: { onClose: () => void }) => {
    const builder = useConsistBuilder();
    const t = useT();
    const { nameOf } = useAssetText();
    const info = useCatalogueLookup(builder);

    // The selected block of cars: anchor is where the selection started, end where shift-click extended it.
    const [selection, setSelection] = useState<{ anchor: number; end: number } | null>(null);
    const [recipeOpen, setRecipeOpen] = useState(false);
    const running = useValue(CONSIST_PREVIEW_RUNNING.binding);

    // One drag for both sources: a car already on the track (from its position) or a new car pulled out of the
    // catalogue (by prefab). "moved" separates a drag from a click: a press and release in place is a click.
    type CarDrag = { from: number; moved: boolean } | { entity: Entity; thumbnail: string; moved: boolean };
    const carDrag = useRef<CarDrag | null>(null);
    const [dragging, setDragging] = useState(false);
    const [dropGap, setDropGap] = useState<number | null>(null);
    const [ghost, setGhost] = useState<{ x: number; y: number; thumbnail: string } | null>(null);
    const suppressClick = useRef(false);
    const suppressAdd = useRef(false);
    // Dragging a car from the train back over the list removes it.
    const removeZone = useRef<HTMLDivElement | null>(null);
    const [carOut, setCarOut] = useState<"none" | "dragging" | "over">("none");
    const drop = useRef<() => void>(() => {});
    const dragMove = useRef<(e: MouseEvent) => void>(() => {});

    // The coupling new cars go into (gap g is in front of car g), set by clicking a coupling.
    const [insertGap, setInsertGap] = useState<number | null>(null);
    const [kind, setKind] = useState("All");
    const [query, setQuery] = useState("");
    const [pack, setPack] = useState<string | null>(null);
    const [author, setAuthor] = useState<string | null>(null);
    // Start on the line's own cars: the full catalogue is every car on this track type, thousands with mods.
    const [scope, setScope] = useState<"Line" | "All">(builder.related.length > 0 ? "Line" : "All");
    const [position, setPosition] = useState(lastPosition);

    // Drag by the header. Listeners go on the document so a fast drag that leaves the header keeps tracking.
    const drag = useRef<{ dx: number; dy: number } | null>(null);
    useEffect(() => {
        const move = (e: MouseEvent) => {
            dragMove.current(e);
            if (!drag.current) return;
            const next = { left: e.clientX - drag.current.dx, top: Math.max(0, e.clientY - drag.current.dy) };
            lastPosition = next;
            setPosition(next);
        };
        const up = () => {
            drag.current = null;
            drop.current();
        };
        document.addEventListener("mousemove", move);
        document.addEventListener("mouseup", up);
        return () => {
            document.removeEventListener("mousemove", move);
            document.removeEventListener("mouseup", up);
        };
    }, []);

    const cars = builder.cars;
    const block = (() => {
        if (!selection) return null;
        const start = Math.min(selection.anchor, selection.end);
        const count = Math.abs(selection.end - selection.anchor) + 1;
        return start + count <= cars.length ? { start, count } : null;
    })();
    const inBlock = (i: number) => block !== null && i >= block.start && i < block.start + block.count;
    const selectBlock = (start: number, count: number) => setSelection({ anchor: start, end: start + count - 1 });

    const clickTile = (i: number, shift: boolean) => {
        if (suppressClick.current) {
            suppressClick.current = false;
            return;
        }
        setInsertGap(null);
        if (shift && selection) setSelection({ anchor: selection.anchor, end: i });
        else if (block && block.count === 1 && block.start === i) setSelection(null);
        else selectBlock(i, 1);
    };

    const clickCoupling = (gap: number) => {
        setSelection(null);
        setInsertGap(insertGap === gap ? null : gap);
    };

    // Moves a block so that it starts at `to` (a position in the train after the block is lifted out).
    const moveBlock = (start: number, count: number, to: number) => {
        const target = Math.max(0, Math.min(cars.length - count, to));
        if (target === start) return;
        consistMove(start, count, target);
        selectBlock(target, count);
    };

    // Inserts a new car into gap `at` and makes it the selection, so the next one lands behind it.
    const insertAt = (entity: Entity, at: number) => {
        consistInsert(entity, at);
        setInsertGap(null);
        selectBlock(at, 1);
    };

    // A click in the catalogue: into the chosen coupling, else behind the selection, else on the end.
    const insertCar = (entity: Entity) => {
        const at = insertGap ?? (block ? block.start + block.count : cars.length);
        insertAt(entity, Math.min(at, cars.length));
    };

    const removeBlock = (start: number, count: number) => {
        consistRemove(start, count);
        setSelection(null);
        setInsertGap(null);
    };

    const overRemoveZone = (x: number, y: number) => {
        const box = removeZone.current?.getBoundingClientRect();
        return !!box && x >= box.left && x <= box.right && y >= box.top && y <= box.bottom;
    };

    const startDrag = (d: CarDrag) => {
        carDrag.current = d;
    };

    dragMove.current = (e: MouseEvent) => {
        const d = carDrag.current;
        if (!d) return;
        if (!d.moved) {
            d.moved = true;
            setDragging(true);
        }
        if ("entity" in d) setGhost({ x: e.clientX, y: e.clientY, thumbnail: d.thumbnail });
    };

    // Dropping: a new car goes into the gap under the pointer; a car from the track moves there, taking the
    // whole selected block with it if the drag started inside the selection.
    drop.current = () => {
        const d = carDrag.current;
        const gap = dropGap;
        carDrag.current = null;
        setDragging(false);
        setDropGap(null);
        setGhost(null);
        if (!d || !d.moved) return;
        // A drag released on the element it started on still fires a click there; swallow it.
        suppressClick.current = "from" in d;
        suppressAdd.current = "entity" in d;
        if (gap === null) return;

        if ("entity" in d) {
            insertAt(d.entity, gap);
            return;
        }

        const moving = block && inBlock(d.from) ? block : { start: d.from, count: 1 };
        if (gap >= moving.start && gap <= moving.start + moving.count) return;
        moveBlock(moving.start, moving.count, gap < moving.start ? gap : gap - moving.count);
    };


    const vanillaPack = t("Consist.VanillaPack", "Base game");
    const packOf = (car: ConsistCatalogueCar) => car.pack || vanillaPack;
    const authorOf = (car: ConsistCatalogueCar) => car.author || vanillaPack;

    const related = useMemo(() => new Set(builder.related.map(entityKey)), [builder.related]);
    const inScope = builder.catalogue.filter((car) => scope === "All" || related.has(entityKey(car.entity)));

    // Each filter's options come from the cars the other filters leave, so no choice leads to an empty list.
    const q = query.trim().toLowerCase();
    const matchesQuery = (car: ConsistCatalogueCar) =>
        q === "" ||
        nameOf(car.id).toLowerCase().includes(q) ||
        car.id.toLowerCase().includes(q) ||
        car.pack.toLowerCase().includes(q) ||
        car.author.toLowerCase().includes(q);
    const passes = (car: ConsistCatalogueCar, skip?: "kind" | "pack" | "author") =>
        matchesQuery(car) &&
        (skip === "kind" || kind === "All" || car.kind === kind) &&
        (skip === "pack" || pack === null || packOf(car) === pack) &&
        (skip === "author" || author === null || authorOf(car) === author);

    const shown = inScope
        .filter((car) => passes(car))
        .sort((a, b) => packOf(a).localeCompare(packOf(b)) || nameOf(a.id).localeCompare(nameOf(b.id)));
    const kindsPresent = KINDS.filter((k) => k === "All" || inScope.some((car) => car.kind === k && passes(car, "kind")));
    // Pack and author list everything installed, not just the current scope: they are how a player finds
    // cars outside the line's own models. Base game sorts first.
    const optionsOf = (of: (car: ConsistCatalogueCar) => string, skip: "pack" | "author") =>
        [...new Set(builder.catalogue.filter((car) => passes(car, skip)).map(of))].sort((a, b) =>
            a === vanillaPack ? -1 : b === vanillaPack ? 1 : a.localeCompare(b),
        );
    const packs = optionsOf(packOf, "pack");
    const authors = optionsOf(authorOf, "author");

    // Choosing a pack or author with nothing on this line widens the scope rather than showing an empty list.
    const choose = (set: (v: string | null) => void, of: (car: ConsistCatalogueCar) => string) => (value: string | null) => {
        set(value);
        if (value !== null && scope === "Line" && !inScope.some((car) => of(car) === value)) setScope("All");
    };
    const [openPick, setOpenPick] = useState<string | null>(null);

    const filtered = kind !== "All" || pack !== null || author !== null || q !== "";
    const clearFilters = () => {
        setKind("All");
        setPack(null);
        setAuthor(null);
        setQuery("");
    };

    // Names need not be unique, but the pick list goes by label, so repeats get a number.
    const configLabels = new Map<string, string>();
    for (const config of builder.configs) {
        let label = config.name;
        for (let n = 2; configLabels.has(label); n++) label = `${config.name} (${n})`;
        configLabels.set(label, config.id);
    }
    const editingLabel = [...configLabels].find(([, id]) => id === builder.editingId)?.[0] ?? builder.editingName;
    const [name, setName] = useState(builder.editingName);
    useEffect(() => setName(builder.editingName), [builder.editingId, builder.editingName]);
    const canSave = cars.length > 0 && !cars.some((car) => car.missing);

    const textButton = (
        label: string,
        onSelect: () => void,
        opts: { on?: boolean; disabled?: boolean; tooltip?: string } = {},
    ) => (
        <VC.ToolButton
            src=""
            selected={opts.on}
            disabled={opts.disabled}
            tooltip={opts.tooltip}
            className={c(VT.toolButton.button, styles.textButton, opts.on ? styles.on : "")}
            onSelect={onSelect}
        >
            {label}
        </VC.ToolButton>
    );


    const status = (() => {
        switch (builder.status) {
            case "NoLead":
                return t("Consist.NoLead", "Add a carriage or multiple-unit car: the train needs one to lead it.");
            case "NoEngine":
                return t("Consist.NoEngine", "A locomotive-hauled train needs an engine.");
            default:
                return null;
        }
    })();

    return (
        <div className={styles.window} style={{ left: `${position.left}px`, top: `${position.top}px` }}>
            {/* The car being dragged in from the list, following the pointer (window-relative). */}
            {ghost && (
                <div
                    className={styles.ghost}
                    style={{
                        left: `${ghost.x - position.left + 8}px`,
                        top: `${ghost.y - position.top + 8}px`,
                        backgroundImage: `url(${ghost.thumbnail})`,
                    }}
                />
            )}
            <div
                className={styles.windowHeader}
                onMouseDown={(e) => {
                    drag.current = { dx: e.clientX - position.left, dy: e.clientY - position.top };
                }}
            >
                <div className={styles.windowTitle}>
                    {t("Consist.Title", "Train builder")}
                    {builder.lineName && <span className={styles.lineName}>{` · ${builder.lineName}`}</span>}
                </div>
                <VC.IconButton
                    tinted={true}
                    src={CLOSE}
                    theme={VT.roundHighlightButton}
                    className={c(VT.panel.closeButton, styles.close)}
                    onSelect={onClose}
                />
            </div>

            <div className={styles.windowBody}>
                <div className={styles.filterBar}>
                    <PickList
                        label={t("Consist.Config", "Saved train")}
                        anyLabel={t("Consist.NewTrain", "New train")}
                        value={builder.editingId ? editingLabel : null}
                        options={[...configLabels.keys()]}
                        onChange={(label) => {
                            consistConfigOpen(label === null ? "" : configLabels.get(label) ?? "");
                            setSelection(null);
                            setInsertGap(null);
                        }}
                        open={openPick === "config"}
                        onOpen={(open) => setOpenPick(open ? "config" : null)}
                    />
                    <input
                        className={styles.search}
                        type="text"
                        value={name}
                        placeholder={t("Consist.Config.Name", "Train name")}
                        onChange={(e) => setName(e.target.value)}
                        onBlur={() => {
                            if (builder.editingId && name.trim() !== "" && name !== builder.editingName) {
                                consistConfigRename(builder.editingId, name);
                            }
                        }}
                    />
                    {builder.editingId !== "" &&
                        textButton(t("Consist.Config.Save", "Save"), () => consistConfigSave(), { disabled: !canSave })}
                    {textButton(t("Consist.Config.SaveNew", "Save as new"), () => consistConfigSaveNew(name), { disabled: !canSave })}
                    {builder.editingId !== "" &&
                        textButton(t("Consist.Config.Delete", "Delete"), () => consistConfigDelete(builder.editingId))}
                </div>

                <div className={styles.sectionTitle}>
                    <div className={styles.sectionLabel}>{t("Consist.Train", "Train")}</div>
                    <Summary builder={builder} t={t} />
                    <div className={styles.zoom}>
                        {textButton(t("Consist.Run", "Run"), () => consistPreviewRun(!running), {
                            on: running,
                            tooltip: t("Consist.Run.Tooltip", "Raise the pantographs, as on a running train"),
                        })}
                        {textButton("-", () => consistPreviewZoom(2), {
                            tooltip: t("Consist.ZoomOut", "Zoom out"),
                        })}
                        {textButton(t("Consist.Fit", "Fit"), () => consistPreviewReset(), {
                            tooltip: t("Consist.Fit.Tooltip", "Fit the whole train in view"),
                        })}
                        {textButton("+", () => consistPreviewZoom(-2), {
                            tooltip: t("Consist.ZoomIn", "Zoom in"),
                        })}
                    </div>
                </div>

                <ConsistStage
                    hint={t("Consist.View3D.Hint", "Drag to slide · right-drag to turn · wheel to zoom · double-click to reset")}
                    cars={cars}
                    nameOf={nameOf}
                    inBlock={inBlock}
                    insertGap={insertGap}
                    dropGap={dropGap}
                    dragging={dragging}
                    onCarMouseDown={(i, shift) => {
                        if (!shift) startDrag({ from: i, moved: false });
                    }}
                    onCarClick={clickTile}
                    onCouplingClick={clickCoupling}
                    onHoverGap={setDropGap}
                    onLeave={() => setDropGap(null)}
                    selection={block}
                    card={
                        block && (
                            <CarCard
                                cars={cars.slice(block.start, block.start + block.count)}
                                t={t}
                                onSet={(stat, value) => consistSetStat(block.start, block.count, stat, value)}
                                onRemove={() => removeBlock(block.start, block.count)}
                            />
                        )
                    }
                    blockFor={(i) => (block && inBlock(i) ? block : { start: i, count: 1 })}
                    onMoveBlock={(start, count, to) => {
                        setInsertGap(null);
                        moveBlock(start, count, to);
                    }}
                    onCarDrag={(dragged, x, y) => {
                        const over = overRemoveZone(x, y);
                        setCarOut(over ? "over" : "dragging");
                        setGhost(over ? { x, y, thumbnail: cars[dragged.start]?.thumbnail ?? "" } : null);
                    }}
                    onCarDrop={(dragged, x, y) => {
                        setCarOut("none");
                        setGhost(null);
                        if (!overRemoveZone(x, y)) return false;
                        // The release also clicks the list row under the pointer; it must not add that car.
                        suppressAdd.current = true;
                        removeBlock(dragged.start, dragged.count);
                        return true;
                    }}
                />

                {cars.length === 0 && (
                    <div className={styles.hint}>
                        {t("Consist.Empty", "Drag cars onto the track, click them in the list below, or start from a model or a running train.")}
                    </div>
                )}

                {block ? (
                    <div className={styles.actions}>
                        <div className={styles.pickedName}>
                            {block.count > 1
                                ? t("Consist.BlockSelected", "{COUNT} cars selected", { COUNT: block.count })
                                : cars[block.start].missing
                                  ? t("Consist.Missing", "Missing asset")
                                  : nameOf(cars[block.start].id)}
                        </div>
                        {textButton("<", () => moveBlock(block.start, block.count, block.start - 1), {
                            disabled: block.start === 0,
                            tooltip: t("Consist.MoveFront", "Move towards the front"),
                        })}
                        {textButton(">", () => moveBlock(block.start, block.count, block.start + 1), {
                            disabled: block.start + block.count >= cars.length,
                            tooltip: t("Consist.MoveBack", "Move towards the back"),
                        })}
                        {textButton(t("Consist.Flip", "Turn"), () => consistFlip(block.start, block.count), {
                            on: block.count === 1 && cars[block.start].reversed,
                            tooltip: t("Consist.Flip.Tooltip", "Turn round: the selected cars swap order and face the other way."),
                        })}
                        {textButton(t("Consist.Repeat", "Repeat"), () => consistRepeat(block.start, block.count, 1), {
                            tooltip: t("Consist.Repeat.Tooltip", "Add another copy of the selected cars straight after them."),
                        })}
                        {textButton(t("Consist.Remove", "Remove"), () => removeBlock(block.start, block.count))}
                    </div>
                ) : (
                    cars.length > 0 && (
                        <div className={styles.hint}>
                            {insertGap !== null
                                ? t("Consist.InsertHint", "New cars go into the highlighted coupling. Click it again to add to the end instead.")
                                : t("Consist.SelectHint", "Click a car to select it, shift-click to select several, drag to move. Click a coupling to choose where new cars go.")}
                        </div>
                    )
                )}

                {status && (
                    <div className={styles.status}>
                        <div className={styles.statusText}>{status}</div>
                    </div>
                )}

                <div className={styles.actions}>
                    <div className={styles.menuAnchor}>
                        {textButton(t("Consist.StartFrom", "Start from model"), () => setRecipeOpen(!recipeOpen), {
                            on: recipeOpen,
                            disabled: builder.models.length === 0,
                            tooltip: t("Consist.StartFrom.Tooltip", "Replace the train with what the game builds from one of the line's selected models."),
                        })}
                        {recipeOpen && (
                            <div className={c(VT.gameDropdown.dropdownMenu, styles.recipeMenu)}>
                                <VC.Scrollable vertical={true} className={styles.pickScroll}>
                                    {builder.models.map((model) => {
                                        const car = info(model);
                                        return (
                                            <div
                                                key={entityKey(model)}
                                                className={c(VT.gameDropdown.dropdownItem, styles.recipeItem)}
                                                onClick={() => {
                                                    consistLoadRecipe(model);
                                                    setRecipeOpen(false);
                                                    setSelection(null);
                                                }}
                                            >
                                                <div
                                                    className={styles.recipeThumb}
                                                    style={{ backgroundImage: `url(${car?.thumbnail ?? ""})` }}
                                                />
                                                <div className={styles.recipeName}>{car ? nameOf(car.id) : "?"}</div>
                                            </div>
                                        );
                                    })}
                                </VC.Scrollable>
                            </div>
                        )}
                    </div>
                    {textButton(t("Consist.CopyRunning", "Copy running train"), () => {
                        consistCopyRunning();
                        setSelection(null);
                    })}
                    {cars.length > 0 &&
                        textButton(t("Consist.Mirror", "Engine at both ends"), () => consistMirror(), {
                            tooltip: t("Consist.Mirror.Tooltip", "Copy the engines at the front to the back of the train, turned round."),
                        })}
                    {cars.length > 1 &&
                        textButton(t("Consist.TurnAll", "Turn whole train"), () => {
                            consistTurnAll();
                            setSelection(null);
                        })}
                    {cars.length > 0 &&
                        textButton(t("Consist.Clear", "Clear"), () => {
                            consistClear();
                            setSelection(null);
                        })}
                </div>

                <div className={styles.sectionTitle}>
                    <div className={styles.sectionLabel}>{t("Consist.AddCars", "Add cars")}</div>
                    <div className={styles.count}>
                        <CountValue value={shown.length} />
                        {" / "}
                        <CountValue value={inScope.length} />
                    </div>
                </div>

                <div className={styles.filterBar}>
                    {textButton(t("Consist.Scope.Line", "This line"), () => setScope("Line"), {
                        on: scope === "Line",
                        disabled: builder.related.length === 0,
                        tooltip: t("Consist.Scope.Line.Tooltip", "The line's selected models, the cars they are built from, and the cars already in this train."),
                    })}
                    {textButton(t("Consist.Scope.All", "All cars"), () => setScope("All"), {
                        on: scope === "All",
                        tooltip: t("Consist.Scope.All.Tooltip", "Every car that can run on this track."),
                    })}
                    <div className={styles.spacer} />
                    <input
                        className={styles.search}
                        type="text"
                        value={query}
                        placeholder={t("Search", "Search...")}
                        onChange={(e) => setQuery(e.target.value)}
                    />
                </div>

                <div className={styles.filterBar}>
                    <PickList
                        label={t("Consist.Pack", "Pack")}
                        anyLabel={t("Consist.AnyPack", "Any pack")}
                        value={pack}
                        options={packs}
                        onChange={choose(setPack, packOf)}
                        open={openPick === "pack"}
                        onOpen={(open) => setOpenPick(open ? "pack" : null)}
                    />
                    <PickList
                        label={t("Consist.Author", "Author")}
                        anyLabel={t("Consist.AnyAuthor", "Any author")}
                        value={author}
                        options={authors}
                        onChange={choose(setAuthor, authorOf)}
                        open={openPick === "author"}
                        onOpen={(open) => setOpenPick(open ? "author" : null)}
                    />
                    {filtered && textButton(t("Consist.ClearFilters", "Clear"), clearFilters)}
                </div>

                <div className={styles.filterBar}>
                    {kindsPresent.map((k) => (
                        <span key={k}>{textButton(t(`Consist.Kind.${k}`, k), () => setKind(k), { on: k === kind })}</span>
                    ))}
                </div>

                <div ref={removeZone} className={styles.catalogueZone}>
                <VC.Scrollable vertical={true} className={styles.catalogue}>
                    {shown.map((car) => (
                        <div
                            key={entityKey(car.entity)}
                            className={styles.catalogueRow}
                            onMouseDown={() => {
                                // A fresh press: anything left over from an earlier drag no longer applies.
                                suppressAdd.current = false;
                                startDrag({ entity: car.entity, thumbnail: car.thumbnail, moved: false });
                            }}
                            onClick={() => {
                                // A drag onto the track already inserted the car; the release is not a click.
                                if (!suppressAdd.current) insertCar(car.entity);
                                suppressAdd.current = false;
                            }}
                        >
                            <div className={styles.rowThumb} style={{ backgroundImage: `url(${car.thumbnail})` }} />
                            <div className={styles.rowName}>
                                <div>{nameOf(car.id)}</div>
                                <div className={styles.rowPack}>{car.author ? `${packOf(car)} · ${car.author}` : packOf(car)}</div>
                            </div>
                            <div className={styles.rowStats}>
                                {car.figures.passengers > 0 && (
                                    <div className={styles.rowStat}>
                                        <CountValue value={car.figures.passengers} />
                                        {` ${t("Consist.Passengers", "passengers")}`}
                                    </div>
                                )}
                                {car.figures.cargo > 0 && (
                                    <div className={styles.rowStat}>
                                        <CountValue value={car.figures.cargo} />
                                        {` ${t("Consist.Cargo", "cargo")}`}
                                    </div>
                                )}
                                <div className={styles.rowStat}>
                                    <LengthValue metres={Math.round(car.length)} />
                                </div>
                            </div>
                            <div className={styles.add}>+</div>
                        </div>
                    ))}
                    {shown.length === 0 && <div className={styles.hint}>{t("Consist.NoMatches", "No cars match.")}</div>}
                </VC.Scrollable>
                {carOut !== "none" && (
                    <div className={c(styles.removeOverlay, carOut === "over" ? styles.removeOverlayOn : "")}>
                        {t("Consist.DropToRemove", "Drop here to remove")}
                    </div>
                )}
                </div>
            </div>
        </div>
    );
};
