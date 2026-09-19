import { useState } from "react";
import { VC } from "vanilla/Components";
import { c } from "utils/classes";
import { thumbnailOf, useFavouritePacks, type StatsLookup, type VehiclePrefab } from "../vehicle-stats";
import { toggleFavouritePack } from "../bindings";
import { VehicleRow, STAR_ON, STAR_OFF } from "./vehicle-row";
import styles from "./vehicle-group.module.scss";

// The Thick* set is used because it is the only one shipping all four directions -- there is a
// StrokeArrowDown/Up but no StrokeArrowRight, so a mixed pair would 404 silently on the closed state.
const INFO_ICON = "Media/Glyphs/Info.svg";
const CARET_OPEN = "Media/Glyphs/ThickStrokeArrowDown.svg";
const CARET_CLOSED = "Media/Glyphs/ThickStrokeArrowRight.svg";

/** A family nested inside a package: the middle tier of the listing. */
export interface VehicleSubGroup {
    /** Stable id for starring the family: placeholder GUID, or pack|title. */
    key: string;
    title: string;
    members: VehiclePrefab[];
    /** The creator's icon for the family, when the pack prefab carries one. */
    icon?: string;
}

export interface VehicleGroupProps {
    /** Already-translated pack name, used as the headline. */
    title: string;
    members: VehiclePrefab[];
    /**
     * Families within this package, when it holds more than one. Rendered instead of the flat
     * member list, giving the pack -> family -> configuration tree the creators asked for.
     */
    subGroups?: VehicleSubGroup[];
    /** Forced open while a search is active, so matches are never hidden behind a shut group. */
    forceOpen: boolean;
    isSelected: (vehicle: VehiclePrefab) => boolean;
    isDisabled: (vehicle: VehiclePrefab) => boolean;
    showSecondary: boolean;
    stats: StatsLookup;
    nameOf: (vehicle: VehiclePrefab) => string;
    onToggle: (vehicle: VehiclePrefab, selected: boolean) => void;
    /**
     * What starring this header pins: the pack id for a pack, the family key for a family within
     * one. Both go into the same starred-groups set, so any tier of the hierarchy can be pinned.
     */
    starKey?: string;
    /** True for a top-level pack, which carries author and date on its header; false for a family. */
    isPack?: boolean;
    /** Opens the upload's page; offered on pack headers when the setting is on. */
    onInfo?: () => void;
    /**
     * The creator's icon for this header, when they set one: a pack prefab's UIObject icon for a
     * family, or the upload's single iconed pack for the upload. Falls back to the first member's
     * thumbnail. Maestro's ask: "it takes this instead of the vehicle inside".
     */
    icon?: string;
    /** Opens a member's page; passed down to every row and family under this header. */
    onRowInfo?: (vehicle: VehiclePrefab) => void;
}

/**
 * One asset pack rendered as a collapsible headline with its vehicles nested beneath, which is the
 * grouping the mockup asks for (goal #3).
 *
 * The headline is drawn from the pack prefab, so the group name is the asset author's own -- the
 * mod invents no taxonomy of its own.
 */
export const VehicleGroup = ({
    title,
    members,
    subGroups,
    forceOpen,
    isSelected,
    isDisabled,
    showSecondary,
    stats,
    nameOf,
    onToggle,
    starKey,
    isPack = false,
    icon,
    onInfo,
    onRowInfo,
}: VehicleGroupProps) => {
    const [open, setOpen] = useState(false);
    const favouritePacks = useFavouritePacks();
    const packStarred = starKey !== undefined && favouritePacks.has(starKey);

    const first = members.length > 0 ? stats(members[0]) : undefined;
    const info = isPack && first
        ? [first.author, first.packageDate ? first.packageDate.slice(0, 10) : ""]
              .filter((s) => s !== "")
              .join("  ·  ")
        : "";

    // A group holding a selection opens on its own, so a shut group never hides what is in use.
    const expanded = forceOpen || open || members.some(isSelected);

    // Variants are plain multi-select checkboxes, the same as the top-level rows and as vanilla:
    // any mix of a family's variants can run on one line. An earlier version drew them as radios
    // and cleared the siblings on pick, which read as a checkbox that unticked things by itself.
    const selectedCount = members.filter(isSelected).length;

    const loose = subGroups
        ? members.filter((m) => !subGroups.some((g) => g.members.includes(m)))
        : members;

    // The header's checkbox works the whole pack: ticked when every pickable member is selected,
    // and a click selects or clears them all. Locked vehicles are skipped when selecting -- vanilla
    // would refuse them one by one anyway -- and a member vanilla will not clear (the last selection
    // on the line) is skipped when clearing, so the pack empties as far as the game allows.
    const pickable = members.filter((v) => !v.locked);
    const allSelected = pickable.length > 0 && pickable.every(isSelected);
    const toggleAll = () => {
        if (allSelected) {
            for (const v of members) {
                if (isSelected(v) && !isDisabled(v)) onToggle(v, false);
            }
        } else {
            for (const v of pickable) {
                if (!isSelected(v)) onToggle(v, true);
            }
        }
    };

    return (
        <>
            <div className={styles.header} onClick={() => setOpen(!open)}>
                <div
                    className={styles.caret}
                    style={{ backgroundImage: `url(${expanded ? CARET_OPEN : CARET_CLOSED})` }}
                />
                {/* Stops propagation so ticking the pack does not also fold it. */}
                <div className={styles.headerCheck} onClick={(e) => e.stopPropagation()}>
                    <VC.Checkbox checked={allSelected} onChange={toggleAll} />
                </div>
                {/* The creator's own icon when they set one on the pack prefab; otherwise the
                    first member's thumbnail stands in, so a shut group still shows what it
                    contains -- creators put livery and formation into these pictures, and a
                    generic glyph threw that away. */}
                {(icon || members.length > 0) && (
                    <div
                        className={styles.icon}
                        style={{ backgroundImage: `url(${icon || thumbnailOf(members[0], stats)})` }}
                    />
                )}
                <div className={styles.titleBlock}>
                    <div className={styles.title}>{title}</div>
                    {/* Pack-level info, only where this group is a pack: who published it and
                        when. Every member reports the same upload, so the first is as good as
                        any. The date is the ISO string's date part; the game exposes no date
                        formatter to mod UI and Intl is not to be relied on in this engine. */}
                    {info && <div className={styles.info}>{info}</div>}
                </div>
                <div className={styles.count}>
                    {selectedCount > 0 ? `${selectedCount}/${members.length}` : `${members.length}`}
                </div>
                {/* The pack page, when the upload has one and the setting is on. Own click, so
                    opening the page never folds the group. */}
                {onInfo && (
                    <div
                        className={styles.infoButton}
                        // Vanilla draws this glyph as a tinted icon: the SVG as a mask over a
                        // colour fill, which is what gives the circled "i" its theme colour.
                        style={{ maskImage: `url(${INFO_ICON})` }}
                        onClick={(e) => {
                            e.stopPropagation();
                            onInfo();
                        }}
                    />
                )}
                {/* The pack star, same treatment as a row's: hover to reveal, lit once set, and
                    its own click so starring never folds the group. */}
                {starKey !== undefined && (
                    <div
                        className={c(styles.star, packStarred ? styles.starOn : "")}
                        style={{ backgroundImage: `url(${packStarred ? STAR_ON : STAR_OFF})` }}
                        onClick={(e) => {
                            e.stopPropagation();
                            toggleFavouritePack(starKey);
                        }}
                    />
                )}
            </div>

            {/* Families first, then any member belonging to none of them as a loose row of this
                group. With the name heuristic every member is in some family, so `loose` is
                empty; with declared families, the untagged siblings of a tagged train land here
                rather than outside the upload. */}
            {expanded && (
                <div className={styles.children}>
                    {subGroups?.map((sub) => (
                        <VehicleGroup
                            key={sub.key}
                            title={sub.title}
                            members={sub.members}
                            forceOpen={forceOpen}
                            isSelected={isSelected}
                            isDisabled={isDisabled}
                            showSecondary={showSecondary}
                            stats={stats}
                            nameOf={nameOf}
                            onToggle={onToggle}
                            starKey={sub.key}
                            icon={sub.icon}
                            onRowInfo={onRowInfo}
                        />
                    ))}
                    {loose.map((vehicle) => (
                        <VehicleRow
                            key={`${vehicle.entity.index}:${vehicle.entity.version}`}
                            vehicle={vehicle}
                            name={nameOf(vehicle)}
                            selected={isSelected(vehicle)}
                            disabled={isDisabled(vehicle)}
                            showSecondary={showSecondary}
                            stats={stats}
                            onToggle={onToggle}
                            variant={true}
                            onInfo={onRowInfo ? () => onRowInfo(vehicle) : undefined}
                        />
                    ))}
                </div>
            )}
        </>
    );
};
