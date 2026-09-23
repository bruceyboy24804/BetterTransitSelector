import { Tooltip } from "cs2/ui";
import { VC } from "vanilla/Components";
import { c } from "utils/classes";
import {
    TARGET_LOAD,
    carriagesText,
    countriesOf,
    flagUrl,
    overhangs,
    projectedLoad,
    thumbnailOf,
    useLineSummary,
    vehiclesNeeded,
    useAssetText,
    useFavourites,
    useStatOptions,
    useT,
    type StatsLookup,
    type VehiclePrefab,
} from "../vehicle-stats";
import { toggleFavourite } from "../bindings";
import { AccelerationValue, CountValue, LengthValue, SpeedValue } from "./stat-value";
import styles from "./vehicle-row.module.scss";

const MULTI_UNIT_ICON = "Media/Game/Icons/MultiUnitTrain.svg";
const WARNING_ICON = "Media/Misc/Warning.svg";
// Unified Icon Library's coloured stars, the same pair Find It uses, so favourites look the same
// across the two mods. UIL (PDX 74417) is a declared dependency in PublishConfiguration.xml.
export const STAR_ON = "coui://uil/Colored/StarFilled.svg";
export const STAR_OFF = "coui://uil/Colored/StarOutline.svg";

/**
 * Images are drawn as a background rather than an <img>, because `object-fit` does not exist in
 * this engine -- a thumbnail that is not square would stretch instead of letterboxing.
 */
const asBackground = (src: string) => ({ backgroundImage: `url(${src})` });

export interface VehicleRowProps {
    vehicle: VehiclePrefab;
    name: string;
    selected: boolean;
    /** True when deselecting this would leave the line with no vehicle at all. */
    disabled: boolean;
    /** Vanilla's showSecondary: false means the line needs no separate locomotive, so the
     *  multi-unit marker carries no meaning here. See the section component. */
    showSecondary: boolean;
    stats: StatsLookup;
    onToggle: (vehicle: VehiclePrefab, selected: boolean) => void;
    /**
     * Rendered as a member of a group: indented and with a divider, so the hierarchy reads.
     */
    variant?: boolean;
    /** Opens the vehicle's page; offered as an info button when given. */
    onInfo?: () => void;
}

const INFO_ICON = "Media/Glyphs/Info.svg";

/**
 * One selectable vehicle. Two-line layout (goal #5) with a large thumbnail (goal #4) and the stats
 * vanilla never sent (goal #6).
 */
export const VehicleRow = ({
    vehicle,
    name,
    selected,
    disabled,
    showSecondary,
    stats,
    onToggle,
    variant = false,
    onInfo,
}: VehicleRowProps) => {
    const vehicleStats = stats(vehicle);
    const options = useStatOptions();
    const favourite = useFavourites().has(vehicle.id);
    const t = useT();

    // Every stat the prefab carries, regardless of which are switched on as row lines: the row
    // shows what the player asked to see at a glance, the tooltip answers the occasional "and
    // how long is it?" without turning that on for every row.
    // The creator's own description, if they wrote one in the editor's Description field. The one
    // place in the game it is shown for a vehicle.
    const description = useAssetText().descriptionOf(vehicle.id);
    const countries = countriesOf(vehicleStats);
    const line = useLineSummary();
    const rowLoad = vehicleStats ? projectedLoad(line, vehicleStats.passengers) : null;
    const tooLong = overhangs(line, vehicleStats);

    const tooltip = vehicleStats && (
        <div className={styles.tip}>
            <div className={styles.tipTitle}>{name}</div>
            {description && <div className={styles.tipDescription}>{description}</div>}
            {vehicleStats.maxSpeed > 0 && (
                <div className={styles.tipRow}>
                    <div className={styles.tipLabel}>{t("MaxSpeed", "Max speed")}</div>
                    <div className={styles.tipValue}><SpeedValue kph={vehicleStats.maxSpeed / 2} /></div>
                </div>
            )}
            {vehicleStats.passengers > 0 && (
                <div className={styles.tipRow}>
                    <div className={styles.tipLabel}>{t("Passengers", "Passengers")}</div>
                    <div className={styles.tipValue}><CountValue value={vehicleStats.passengers} /></div>
                </div>
            )}
            {/* What this line would look like on this stock: today's riders spread over the fleet
                the line wants, all of this model. The question a player is actually asking when
                they hover a row -- "would this cope?" -- answered before they pick it. */}
            {rowLoad !== null && (
                <div className={styles.tipRow}>
                    <div className={styles.tipLabel}>{t("LoadOnThisLine", "Load on this line")}</div>
                    <div className={c(styles.tipValue, rowLoad > TARGET_LOAD ? styles.tipOverloaded : "")}>
                        {`${Math.round(rowLoad * 100)}%`}
                        {rowLoad > TARGET_LOAD &&
                            " · " + t("NeedsVehicles", "needs {COUNT} vehicles", { COUNT: vehiclesNeeded(line, vehicleStats.passengers) })}
                    </div>
                </div>
            )}
            {vehicleStats.acceleration > 0 && (
                <div className={styles.tipRow}>
                    <div className={styles.tipLabel}>{t("Acceleration", "Acceleration")}</div>
                    <div className={styles.tipValue}><AccelerationValue mps2={vehicleStats.acceleration} /></div>
                </div>
            )}
            {vehicleStats.braking > 0 && (
                <div className={styles.tipRow}>
                    <div className={styles.tipLabel}>{t("Braking", "Braking")}</div>
                    <div className={styles.tipValue}><AccelerationValue mps2={vehicleStats.braking} /></div>
                </div>
            )}
            {vehicleStats.energyType && (
                <div className={styles.tipRow}>
                    <div className={styles.tipLabel}>{t("Power", "Power")}</div>
                    <div className={styles.tipValue}>{vehicleStats.energyType}</div>
                </div>
            )}
            {vehicleStats.carriages > 0 && (
                <div className={styles.tipRow}>
                    <div className={styles.tipLabel}>{t("Carriages", "Carriages")}</div>
                    <div className={styles.tipValue}>{carriagesText(vehicleStats)}</div>
                </div>
            )}
            {vehicleStats.length > 0 && (
                <div className={styles.tipRow}>
                    <div className={styles.tipLabel}>{t("Length", "Length")}</div>
                    <div className={c(styles.tipValue, tooLong ? styles.tipOverloaded : "")}>
                        <LengthValue metres={vehicleStats.length} />
                        {line.platformLength > 0 && (
                            <>
                                {" · "}
                                {t("Platform", "platform")}
                                {" "}
                                <LengthValue metres={line.platformLength} />
                            </>
                        )}
                    </div>
                </div>
            )}
            {vehicleStats.author && (
                <div className={styles.tipRow}>
                    <div className={styles.tipLabel}>{t("Author", "Author")}</div>
                    <div className={styles.tipValue}>{vehicleStats.author}</div>
                </div>
            )}
            {vehicleStats.groupTitle && (
                <div className={styles.tipRow}>
                    <div className={styles.tipLabel}>{t("Pack", "Pack")}</div>
                    <div className={styles.tipValue}>{vehicleStats.groupTitle}</div>
                </div>
            )}
            {/* Where the creator says it runs, as flags only -- REV0's spec: "one line under
                Pack, represented via country flag icons". Names were tried beside them and
                dropped again; the vehicle page still lists them. */}
            {countries.length > 0 && (
                <div className={styles.tipRow}>
                    <div className={styles.tipLabel}>{t("Countries", "Countries")}</div>
                    <div className={styles.tipValue}>
                        {countries.map((iso) => (
                            <div key={iso} className={styles.flag} style={asBackground(flagUrl(iso))} />
                        ))}
                    </div>
                </div>
            )}
        </div>
    );

    return (
        // The whole row is the hit target: a click anywhere on it toggles the checkbox, so the
        // player is not asked to aim at a 20rem box in a list they are scanning by name and icon.
        <Tooltip tooltip={tooltip} disabled={!tooltip} delayTime={400}>
        <div
            className={c(
                styles.row,
                variant ? styles.variantRow : "",
                vehicle.locked ? styles.locked : "",
                disabled ? styles.disabled : "",
            )}
            onClick={() => !disabled && onToggle(vehicle, !selected)}
        >
            {/* The same checkbox for variants and top-level rows. Disabled means "this is the
                last selection on the line", which vanilla refuses to clear.

                The wrapper swallows the click so the row's handler does not fire as well --
                otherwise the checkbox toggles twice and lands where it started. */}
            <div onClick={(e) => e.stopPropagation()}>
                <VC.Checkbox
                    checked={selected}
                    disabled={disabled}
                    onChange={() => onToggle(vehicle, !selected)}
                />
            </div>

            {/* On every row, variants included. An earlier version hid it inside a family on the
                theory that the header's picture stood for all members -- true for liveries of
                one train, wrong for a bus pack of seven different models, and moot now that a
                creator can give each row its own icon. */}
            <div className={styles.thumb} style={asBackground(thumbnailOf(vehicle, stats))} />

            {vehicle.objectRequirementIcons?.map((icon, i) => (
                <div key={i} className={styles.requirementIcon} style={asBackground(icon)} />
            ))}

            <div className={styles.name}>{name}</div>

            {/* Longer than the line's shortest platform. The game still lets it stop -- it
                overhangs -- so this is a warning for the player who cares how it looks, and
                the tooltip carries the numbers. */}
            {tooLong && <div className={styles.warning} style={asBackground(WARNING_ICON)} />}

            {/* The table is published once per load, so a row can render before it arrives -- and a
                modded vehicle may legitimately have no row at all. Drop the block rather than
                printing a misleading "0 km/h". */}
            {vehicleStats && (
                <div className={styles.stats}>
                    {/* Numbers go through the game's own formatters, so they follow the player's
                        unit system and locale: length in metres or feet, digit grouping as the rest
                        of the UI does it. Speed is the exception -- see SpeedValue.

                        Every line is gated on BOTH the setting and the value: a stat the player
                        asked for but the prefab does not carry would otherwise render as a bare
                        "Passengers: 0", which reads as a fact rather than as missing data. */}
                    {options.maxSpeed && vehicleStats.maxSpeed > 0 && (
                        <div className={styles.stat}>
                            {t("MaxSpeed", "Max speed") + ": "}
                            <SpeedValue kph={vehicleStats.maxSpeed / 2} />
                        </div>
                    )}
                    {options.passengers && vehicleStats.passengers > 0 && (
                        <div className={styles.stat}>
                            {t("Passengers", "Passengers") + ": "}
                            <CountValue value={vehicleStats.passengers} />
                        </div>
                    )}
                    {options.acceleration && vehicleStats.acceleration > 0 && (
                        <div className={styles.stat}>
                            {t("Acceleration", "Acceleration") + ": "}
                            <AccelerationValue mps2={vehicleStats.acceleration} />
                        </div>
                    )}
                    {options.braking && vehicleStats.braking > 0 && (
                        <div className={styles.stat}>
                            {t("Braking", "Braking") + ": "}
                            <AccelerationValue mps2={vehicleStats.braking} />
                        </div>
                    )}
                    {options.energyType && vehicleStats.energyType !== "" && (
                        <div className={styles.stat}>{t("Power", "Power") + ": " + vehicleStats.energyType}</div>
                    )}
                    {options.carriages && vehicleStats.carriages > 0 && (
                        <div className={styles.stat}>
                            {t("Carriages", "Carriages") + ": " + carriagesText(vehicleStats)}
                        </div>
                    )}
                    {options.length && vehicleStats.length > 0 && (
                        <div className={styles.stat}>
                            {t("Length", "Length") + ": "}
                            <LengthValue metres={vehicleStats.length} />
                        </div>
                    )}
                </div>
            )}

            {vehicle.multiunit && showSecondary && (
                <div className={styles.multiUnit} style={asBackground(MULTI_UNIT_ICON)} />
            )}

            {/* The vehicle page, the pack header's info button one tier down: hover to reveal,
                own click so it never toggles the row. */}
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

            {/* The star, Find It's way: shown on hover, always shown once starred. Its own click
                so starring never toggles the row's selection. The game ships both glyphs. */}
            <div
                className={c(styles.star, favourite ? styles.starOn : "")}
                style={asBackground(favourite ? STAR_ON : STAR_OFF)}
                onClick={(e) => {
                    e.stopPropagation();
                    toggleFavourite(vehicle.id);
                }}
            />
        </div>
        </Tooltip>
    );
};
