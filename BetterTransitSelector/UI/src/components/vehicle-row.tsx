import { Tooltip } from "cs2/ui";
import { VC } from "vanilla/Components";
import { c } from "utils/classes";
import {
    TARGET_LOAD,
    carriagesText,
    countriesOf,
    flagUrl,
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

export const STAR_ON = "coui://uil/Colored/StarFilled.svg";
export const STAR_OFF = "coui://uil/Colored/StarOutline.svg";

const asBackground = (src: string) => ({ backgroundImage: `url(${src})` });

export interface VehicleRowProps {
    vehicle: VehiclePrefab;
    name: string;
    selected: boolean;

    disabled: boolean;

    showSecondary: boolean;
    stats: StatsLookup;
    onToggle: (vehicle: VehiclePrefab, selected: boolean) => void;

    variant?: boolean;

    onInfo?: () => void;
}

const INFO_ICON = "Media/Glyphs/Info.svg";

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

    const description = useAssetText().descriptionOf(vehicle.id);
    const countries = countriesOf(vehicleStats);
    const line = useLineSummary();
    const rowLoad = vehicleStats ? projectedLoad(line, vehicleStats.passengers) : null;

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
                    <div className={styles.tipValue}><LengthValue metres={vehicleStats.length} /></div>
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

            <div onClick={(e) => e.stopPropagation()}>
                <VC.Checkbox
                    checked={selected}
                    disabled={disabled}
                    onChange={() => onToggle(vehicle, !selected)}
                />
            </div>

            <div className={styles.thumb} style={asBackground(thumbnailOf(vehicle, stats))} />

            {vehicle.objectRequirementIcons?.map((icon, i) => (
                <div key={i} className={styles.requirementIcon} style={asBackground(icon)} />
            ))}

            <div className={styles.name}>{name}</div>

            {vehicleStats && (
                <div className={styles.stats}>

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
