import { useState } from "react";
import { trigger } from "cs2/api";
import { VC } from "vanilla/Components";
import { c } from "utils/classes";
import { entityKey, useConsistBuilder, useT, type ConsistConfigRow, type Entity, type VehiclePrefab } from "../vehicle-stats";
import { consistApply, consistApplyDefault } from "../bindings";
import { CountValue, SpeedValue } from "./stat-value";
import groupStyles from "./vehicle-group.module.scss";
import rowStyles from "./vehicle-row.module.scss";
import styles from "./custom-trains-group.module.scss";

// Model selection is only ever changed through vanilla's own trigger, never by the mod.
const VANILLA_GROUP = "SelectVehiclesSection";
const NULL_ENTITY: Entity = { index: 0, version: 0 };
const CARET_OPEN = "Media/Glyphs/ThickStrokeArrowDown.svg";
const CARET_CLOSED = "Media/Glyphs/ThickStrokeArrowRight.svg";

const isNull = (entity: Entity) => entity.index === 0;

const SimpleRow = ({ name, checked, onPick }: { name: string; checked: boolean; onPick: () => void }) => (
    <div className={rowStyles.row}>
        <VC.Checkbox checked={checked} onChange={onPick} />
        <div className={rowStyles.name}>{name}</div>
    </div>
);

export interface CustomTrainsGroupProps {
    query: string;
    selectedPrimary: VehiclePrefab[];
    selectedSecondary: VehiclePrefab[] | null;
}

/** The player's saved trains, as single-choice rows at the top of the primary dropdown. Rail lines only. */
export const CustomTrainsGroup = ({ query, selectedPrimary, selectedSecondary }: CustomTrainsGroupProps) => {
    const builder = useConsistBuilder();
    const t = useT();
    const [open, setOpen] = useState(true);
    if (!builder.valid) return null;

    const q = query.trim().toLowerCase();
    const rows = builder.configs.filter((row) => q === "" || row.name.toLowerCase().includes(q));
    if (q !== "" && rows.length === 0) return null;

    const pick = (row: ConsistConfigRow) => {
        // Selected first, so vanilla never sees the train without its models.
        const has = (list: VehiclePrefab[] | null, entity: Entity) => (list ?? []).some((v) => entityKey(v.entity) === entityKey(entity));
        if (!isNull(row.lead) && !has(selectedPrimary, row.lead)) {
            trigger(VANILLA_GROUP, "selectVehicles", row.lead, NULL_ENTITY);
        }
        if (!isNull(row.engine) && !has(selectedSecondary, row.engine)) {
            trigger(VANILLA_GROUP, "selectVehicles", NULL_ENTITY, row.engine);
        }
        consistApply(row.id);
    };

    return (
        <>
            <div className={groupStyles.header} onClick={() => setOpen(!open)}>
                <div
                    className={groupStyles.caret}
                    style={{ backgroundImage: `url(${open || q !== "" ? CARET_OPEN : CARET_CLOSED})` }}
                />
                <div className={groupStyles.titleBlock}>
                    <div className={groupStyles.title}>{t("Consist.Configs", "Custom trains")}</div>
                </div>
            </div>

            {(open || q !== "") && (
                <div className={styles.rows}>
                    {q === "" && builder.lineState === "Modified" && (
                        <SimpleRow name={t("Consist.Modified", "Modified train")} checked={true} onPick={() => {}} />
                    )}
                    {q === "" && (
                        <SimpleRow
                            name={t("Consist.Vanilla", "Game default")}
                            checked={builder.lineState === "Default"}
                            onPick={() => consistApplyDefault()}
                        />
                    )}
                    {rows.map((row) => {
                        const cantLead = row.canLead ? null : t("Consist.CantLead", "No car can lead this train");
                        const reason = row.missing ? t("Consist.Missing", "Missing asset") : cantLead;
                        return (
                            <div key={row.id} className={c(rowStyles.row, reason ? styles.disabled : "")}>
                                <VC.Checkbox
                                    checked={row.selected}
                                    disabled={reason !== null}
                                    onChange={() => {
                                        if (reason === null && !row.selected) pick(row);
                                    }}
                                />
                                <div className={rowStyles.thumb} style={{ backgroundImage: `url(${row.thumbnail})` }} />
                                <div className={rowStyles.name}>
                                    <div>{row.name}</div>
                                    {reason && <div className={styles.reason}>{reason}</div>}
                                </div>
                                <div className={rowStyles.stats}>
                                    <div className={rowStyles.stat}>
                                        <CountValue value={row.cars} />
                                        {` ${t("Consist.Cars", "cars")}`}
                                    </div>
                                    <div className={rowStyles.stat}>
                                        {row.cargo > 0 && row.passengers === 0 ? (
                                            <>
                                                <CountValue value={row.cargo} />
                                                {` ${t("Consist.Cargo", "cargo")}`}
                                            </>
                                        ) : (
                                            <>
                                                <CountValue value={row.passengers} />
                                                {` ${t("Consist.Passengers", "passengers")}`}
                                            </>
                                        )}
                                    </div>
                                    {row.maxSpeed > 0 && (
                                        <div className={rowStyles.stat}>
                                            <SpeedValue kph={Math.round(row.maxSpeed)} />
                                        </div>
                                    )}
                                </div>
                            </div>
                        );
                    })}
                    {builder.otherModels && (
                        <div className={styles.warning}>
                            {t("Consist.OtherModels", "The line also has lead models selected that aren't in this train. Trains of those models already parked in the depot are sent out unchanged; deselect them to always get this train.")}
                        </div>
                    )}
                </div>
            )}
            <div className={styles.divider} />
        </>
    );
};
