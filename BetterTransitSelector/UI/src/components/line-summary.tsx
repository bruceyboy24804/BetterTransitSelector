import { VC } from "vanilla/Components";
import { c } from "utils/classes";
import {
    TARGET_LOAD,
    projectedLoad,
    useLineSummary,
    useT,
    vehiclesNeeded,
    type StatsLookup,
    type VehiclePrefab,
} from "../vehicle-stats";
import { CountValue } from "./stat-value";
import styles from "./line-summary.module.scss";

export interface LineSummaryRowProps {
    /** The primary selection: what the line spawns. */
    selected: VehiclePrefab[];
    stats: StatsLookup;
}

/**
 * What the current mix means for the line, in the panel's own InfoRow style beneath the picker.
 *
 * Fleet is the game's number -- how many vehicles the line wants for its interval and loop time
 * -- and is independent of the mix: the route is planned at a 1000 km/h cap, so track limits set
 * the loop time and a faster train does not shrink the fleet. It is here so the player sees the
 * consequence of a capacity choice next to the choice, not to suggest speed changes it.
 *
 * Capacity per departure is a range when the mix is mixed: the game picks a model at random for
 * each vehicle it spawns, so the honest figure is "between the smallest and the largest".
 */
export const LineSummaryRow = ({ selected, stats }: LineSummaryRowProps) => {
    const line = useLineSummary();
    const t = useT();
    if (!line.valid || selected.length === 0) return null;

    const capacities = selected
        .map((v) => stats(v)?.passengers ?? 0)
        .filter((n) => n > 0);
    if (capacities.length === 0) return null;

    const min = Math.min(...capacities);
    const max = Math.max(...capacities);
    const avg = capacities.reduce((a, b) => a + b, 0) / capacities.length;
    const fleet = line.targetCount;
    const loadNow = line.capacityNow > 0 ? line.riders / line.capacityNow : 0;
    const projected = projectedLoad(line, avg);

    return (
        <>
            <VC.InfoRow
                left={t("Fleet", "Fleet")}
                right={
                    <div className={styles.value}>
                        <CountValue value={line.vehicleCount} />
                        {" / "}
                        <CountValue value={fleet} />
                    </div>
                }
                tooltip={t("Fleet.Tooltip", "Vehicles on the line now, and how many the line wants for its interval and loop time. Set by the route, not by the vehicle model.")}
            />
            <VC.InfoRow
                left={t("PerDeparture", "Per departure")}
                right={
                    <div className={styles.value}>
                        <CountValue value={min} />
                        {min !== max && (
                            <>
                                {" – "}
                                <CountValue value={max} />
                            </>
                        )}
                    </div>
                }
                tooltip={t("PerDeparture.Tooltip", "Passengers one vehicle can carry. A range when several models are selected, since the game picks one at random for each vehicle it sends.")}
            />
            <VC.InfoRow
                left={t("FleetCapacity", "Fleet capacity")}
                right={
                    <div className={styles.value}>
                        <CountValue value={Math.round(avg * fleet)} />
                    </div>
                }
                tooltip={t("FleetCapacity.Tooltip", "Passengers the whole fleet carries at once, at the average capacity of the selected models.")}
            />

            {/* Demand against supply. "Now" is the game's own usage figure (riders over the seats
                actually on the line); "with this mix" spreads the same riders over the fleet the
                line wants at the selected models' average capacity. The difference is what the
                selection changes. Coloured past the comfortable ceiling, since that is the
                moment to pick bigger stock or raise the vehicle count. */}
            {line.capacityNow > 0 && (
                <VC.InfoRow
                    left={t("LoadNow", "Load now")}
                    right={
                        <div className={c(styles.value, loadNow > TARGET_LOAD ? styles.overloaded : "")}>
                            <CountValue value={line.riders} />
                            {" / "}
                            <CountValue value={line.capacityNow} />
                            {` (${Math.round(loadNow * 100)}%)`}
                        </div>
                    }
                    tooltip={t("LoadNow.Tooltip", "Passengers aboard right now against the seats on the line's current vehicles. The same figure as the transport overview's usage.")}
                />
            )}
            {projected !== null && (
                <VC.InfoRow
                    left={t("LoadProjected", "Load with this mix")}
                    right={
                        <div className={c(styles.value, projected > TARGET_LOAD ? styles.overloaded : "")}>
                            {`${Math.round(projected * 100)}%`}
                            {projected > TARGET_LOAD && (
                                <span className={styles.hint}>
                                    {" · "}
                                    {t("NeedsVehicles", "needs {COUNT} vehicles", { COUNT: vehiclesNeeded(line, avg) })}
                                </span>
                            )}
                        </div>
                    }
                    tooltip={t("LoadProjected.Tooltip", "Today's riders spread over the fleet the line wants, if every vehicle were the selected models' average. Over 80% the line is crowded: pick bigger stock or raise the vehicle count to the number shown.")}
                />
            )}
        </>
    );
};
