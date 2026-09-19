import { Tooltip } from "cs2/ui";
import { VC, VT } from "vanilla/Components";
import { c } from "utils/classes";
import {
    TARGET_LOAD,
    countriesOf,
    flagUrl,
    overhangs,
    projectedLoad,
    thumbnailOf,
    useAssetInfo,
    useAssetText,
    useCountryName,
    useLineSummary,
    useT,
    vehiclesNeeded,
    type StatsLookup,
    type VehiclePrefab,
} from "../vehicle-stats";
import { AccelerationValue, CountValue, LengthValue, SpeedValue } from "./stat-value";
import { VehicleRow } from "./vehicle-row";
import { sizeOf } from "./pack-page";
import styles from "./pack-page.module.scss";
import own from "./vehicle-page.module.scss";

export interface VehiclePageProps {
    vehicle: VehiclePrefab;
    stats: StatsLookup;
    nameOf: (vehicle: VehiclePrefab) => string;
    isSelected: (vehicle: VehiclePrefab) => boolean;
    isDisabled: (vehicle: VehiclePrefab) => boolean;
    showSecondary: boolean;
    onToggle: (vehicle: VehiclePrefab, selected: boolean) => void;
    /** Opens the upload's page, when the vehicle belongs to one. */
    onOpenPack?: () => void;
    onClose: () => void;
}

const dateOf = (iso: string) => (iso ? iso.slice(0, 10) : "");


/**
 * One vehicle's page, the tier under the pack page: the big picture, the creator's description,
 * every stat, the consist car by car, where it runs, how it would do on this line, and the
 * file it came from. Nothing here is requested from anywhere -- the file block is the asset
 * database's own metadata and the rest is the stats table the rows already use.
 */
export const VehiclePage = ({
    vehicle,
    stats,
    nameOf,
    isSelected,
    isDisabled,
    showSecondary,
    onToggle,
    onOpenPack,
    onClose,
}: VehiclePageProps) => {
    const t = useT();
    const info = useAssetInfo();
    const text = useAssetText();
    const countryName = useCountryName();
    const line = useLineSummary();
    const s = stats(vehicle);

    // Same rule as the pack page: the binding holds whichever asset was last asked for.
    const ready = info.valid && info.id === vehicle.id;

    const name = nameOf(vehicle);
    const description = text.descriptionOf(vehicle.id);
    const countries = s ? countriesOf(s) : [];
    const load = s ? projectedLoad(line, s.passengers) : null;
    const tooLong = overhangs(line, s);

    const Row = ({ label, children, warn }: { label: string; children: React.ReactNode; warn?: boolean }) => (
        <div className={own.statRow}>
            <div className={own.statLabel}>{label}</div>
            <div className={c(own.statValue, warn ? styles.missing : "")}>{children}</div>
        </div>
    );

    return (
        <div className={styles.page}>
            <div className={styles.header}>
                <div className={styles.title}>{name}</div>
                <VC.IconButton
                    tinted={true}
                    src="Media/Glyphs/Close.svg"
                    theme={VT.roundHighlightButton}
                    className={c(VT.panel.closeButton, styles.close)}
                    onSelect={onClose}
                />
            </div>

            {/* The thumbnail at a size the creators' formation diagrams are legible at -- the
                whole reason goal #4 exists, and the one place it can be shown big. */}
            <div className={own.picture} style={{ backgroundImage: `url(${thumbnailOf(vehicle, stats)})` }} />

            {s && (s.author || s.groupTitle) && (
                <div className={styles.meta}>
                    {s.author && <span>{s.author}</span>}
                    {s.groupTitle && (
                        <span className={c(styles.dim, onOpenPack ? own.packLink : "")} onClick={onOpenPack}>
                            {(s.author ? " · " : "") + s.groupTitle}
                        </span>
                    )}
                    {s.packageDate && <span className={styles.dim}>{" · " + dateOf(s.packageDate)}</span>}
                </div>
            )}

            {countries.length > 0 && (
                <div className={styles.flags}>
                    {countries.map((iso) => (
                        <Tooltip key={iso} tooltip={countryName(iso)}>
                            <div className={styles.flag} style={{ backgroundImage: `url(${flagUrl(iso)})` }} />
                        </Tooltip>
                    ))}
                </div>
            )}

            {description && <div className={own.description}>{description}</div>}

            {s && (
                <div className={styles.section}>
                    <div className={styles.sectionHeading}>{t("Stats", "Stats")}</div>
                    {s.maxSpeed > 0 && <Row label={t("MaxSpeed", "Max speed")}><SpeedValue kph={s.maxSpeed / 2} /></Row>}
                    {s.passengers > 0 && <Row label={t("Passengers", "Passengers")}><CountValue value={s.passengers} /></Row>}
                    {s.acceleration > 0 && <Row label={t("Acceleration", "Acceleration")}><AccelerationValue mps2={s.acceleration} /></Row>}
                    {s.braking > 0 && <Row label={t("Braking", "Braking")}><AccelerationValue mps2={s.braking} /></Row>}
                    {s.energyType && <Row label={t("Power", "Power")}>{s.energyType}</Row>}
                    {s.carriages > 0 && <Row label={t("Carriages", "Carriages")}><CountValue value={s.carriages} /></Row>}
                    {s.length > 0 && (
                        <Row label={t("Length", "Length")} warn={tooLong}>
                            <LengthValue metres={s.length} />
                            {line.platformLength > 0 && (
                                <>
                                    {" · "}
                                    {t("Platform", "platform")}
                                    {" "}
                                    <LengthValue metres={line.platformLength} />
                                </>
                            )}
                        </Row>
                    )}
                    {s.theme && <Row label={t("Theme", "Theme")}>{s.theme}</Row>}
                    {load !== null && (
                        <Row label={t("LoadOnThisLine", "Load on this line")} warn={load > TARGET_LOAD}>
                            {`${Math.round(load * 100)}%`}
                            {load > TARGET_LOAD &&
                                " · " + t("NeedsVehicles", "needs {COUNT} vehicles", { COUNT: vehiclesNeeded(line, s.passengers) })}
                        </Row>
                    )}
                </div>
            )}

            {/* The consist, car by car: where the headline passengers and length come from.
                Only for a multi-car prefab; a bus is its own one car and the table would say
                nothing the stats block does not. */}
            {ready && info.cars.length > 1 && (
                <div className={styles.section}>
                    <div className={styles.sectionHeading}>{t("Consist", "Consist")}</div>
                    {info.cars.map((car, i) => (
                        <div key={i} className={own.car}>
                            <div className={own.carCount}>{`${car.count}×`}</div>
                            <div className={own.carName}>{text.nameOf(car.id)}</div>
                            <div className={own.carStat}>
                                {car.passengers > 0 ? <CountValue value={car.passengers} /> : "—"}
                            </div>
                            <div className={own.carStat}>
                                {car.length > 0 ? <LengthValue metres={Math.round(car.length)} /> : ""}
                            </div>
                        </div>
                    ))}
                </div>
            )}

            {ready && info.fileName && (
                <div className={styles.section}>
                    <div className={styles.sectionHeading}>{t("AssetFile", "Asset file")}</div>
                    <Row label={t("File", "File")}>{info.fileName}</Row>
                    {info.size > 0 && <Row label={t("Size", "Size")}>{sizeOf(info.size)}</Row>}
                    {info.modified && <Row label={t("Modified", "Modified")}>{dateOf(info.modified)}</Row>}
                    {info.own && <Row label={t("Source", "Source")}>{t("OwnAsset", "Your own asset")}</Row>}
                    <Row label={t("PrefabId", "Prefab id")}>{vehicle.id}</Row>
                </div>
            )}

            <div className={styles.section}>
                <VehicleRow
                    vehicle={vehicle}
                    name={name}
                    selected={isSelected(vehicle)}
                    disabled={isDisabled(vehicle)}
                    showSecondary={showSecondary}
                    stats={stats}
                    onToggle={onToggle}
                />
            </div>
        </div>
    );
};
