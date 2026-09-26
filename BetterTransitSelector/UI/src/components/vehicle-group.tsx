import { useState } from "react";
import { VC } from "vanilla/Components";
import { c } from "utils/classes";
import { thumbnailOf, useFavouritePacks, type StatsLookup, type VehiclePrefab } from "../vehicle-stats";
import { toggleFavouritePack } from "../bindings";
import { VehicleRow, STAR_ON, STAR_OFF } from "./vehicle-row";
import styles from "./vehicle-group.module.scss";

const INFO_ICON = "Media/Glyphs/Info.svg";
const CARET_OPEN = "Media/Glyphs/ThickStrokeArrowDown.svg";
const CARET_CLOSED = "Media/Glyphs/ThickStrokeArrowRight.svg";

export interface VehicleSubGroup {
    key: string;
    title: string;
    members: VehiclePrefab[];

    icon?: string;
}

export interface VehicleGroupProps {
    title: string;
    members: VehiclePrefab[];

    subGroups?: VehicleSubGroup[];

    forceOpen: boolean;
    isSelected: (vehicle: VehiclePrefab) => boolean;
    isDisabled: (vehicle: VehiclePrefab) => boolean;
    showSecondary: boolean;
    stats: StatsLookup;
    nameOf: (vehicle: VehiclePrefab) => string;
    onToggle: (vehicle: VehiclePrefab, selected: boolean) => void;

    starKey?: string;

    isPack?: boolean;

    onInfo?: () => void;

    icon?: string;

    onRowInfo?: (vehicle: VehiclePrefab) => void;
}

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

    const expanded = forceOpen || open || members.some(isSelected);

    const selectedCount = members.filter(isSelected).length;

    const loose = subGroups
        ? members.filter((m) => !subGroups.some((g) => g.members.includes(m)))
        : members;

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

                <div className={styles.headerCheck} onClick={(e) => e.stopPropagation()}>
                    <VC.Checkbox checked={allSelected} onChange={toggleAll} />
                </div>

                {(icon || members.length > 0) && (
                    <div
                        className={styles.icon}
                        style={{ backgroundImage: `url(${icon || thumbnailOf(members[0], stats)})` }}
                    />
                )}
                <div className={styles.titleBlock}>
                    <div className={styles.title}>{title}</div>

                    {info && <div className={styles.info}>{info}</div>}
                </div>
                <div className={styles.count}>
                    {selectedCount > 0 ? `${selectedCount}/${members.length}` : `${members.length}`}
                </div>

                {onInfo && (
                    <div
                        className={styles.infoButton}

                        style={{ maskImage: `url(${INFO_ICON})` }}
                        onClick={(e) => {
                            e.stopPropagation();
                            onInfo();
                        }}
                    />
                )}

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
