import { trigger } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { VC } from "vanilla/Components";
import { useVehicleName, useVehicleStats, type VehiclePrefab } from "../vehicle-stats";
import { recordUsed } from "../bindings";
import { VehicleDropdown } from "./vehicle-dropdown";
import { LineSummaryRow } from "./line-summary";

const VANILLA_GROUP = "SelectVehiclesSection";
const SELECT = "selectVehicles";
const DESELECT = "deselectVehicles";

const NULL_ENTITY = { index: 0, version: 0 };

const LABEL_PRIMARY = "SelectedInfoPanel.SELECT_VEHICLE_PRIMARY";
const LABEL_SECONDARY = "SelectedInfoPanel.SELECT_VEHICLE_SECONDARY";

export interface SelectVehiclesSectionProps {
    routePrefab: string;
    selectedPrimaryVehicles: VehiclePrefab[];

    selectedSecondaryVehicles: VehiclePrefab[] | null;
    availablePrimaryVehicles: VehiclePrefab[];
    availableSecondaryVehicles: VehiclePrefab[] | null;
    group?: string;
    tooltipKeys?: unknown;
    tooltipTags?: unknown;
}

export const SelectVehiclesSection = (props: SelectVehiclesSectionProps) => {
    const {
        routePrefab,
        selectedPrimaryVehicles,
        selectedSecondaryVehicles,
        availablePrimaryVehicles,
        availableSecondaryVehicles,
    } = props;

    const stats = useVehicleStats();
    const nameOf = useVehicleName();
    const { translate } = useLocalization();

    const showSecondary =
        (availableSecondaryVehicles?.length ?? 0) > 0 &&
        selectedPrimaryVehicles.some((v) => !v.multiunit);

    const toggle = (isPrimary: boolean) => (vehicle: VehiclePrefab, select: boolean) => {
        const primary = isPrimary ? vehicle.entity : NULL_ENTITY;
        const secondary = isPrimary ? NULL_ENTITY : vehicle.entity;
        trigger(VANILLA_GROUP, select ? SELECT : DESELECT, primary, secondary);

        if (select) recordUsed(vehicle.id);
    };

    const label = (id: string, fallback: string) =>
        translate(`${id}[${routePrefab}]`, fallback) ?? fallback;

    return (
        <VC.InfoSection disableFocus={true}>
            <VehicleDropdown
                label={label(LABEL_PRIMARY, "Select vehicle model")}
                available={availablePrimaryVehicles}
                selected={selectedPrimaryVehicles}
                isPrimary={true}
                showSecondary={showSecondary}
                stats={stats}
                nameOf={nameOf}
                onToggle={toggle(true)}
            />

            {showSecondary && availableSecondaryVehicles && selectedSecondaryVehicles && (
                <VehicleDropdown
                    label={label(LABEL_SECONDARY, "Select carriage model")}
                    available={availableSecondaryVehicles}
                    selected={selectedSecondaryVehicles}
                    isPrimary={false}
                    showSecondary={showSecondary}
                    stats={stats}
                    nameOf={nameOf}
                    onToggle={toggle(false)}
                />
            )}

            <LineSummaryRow selected={selectedPrimaryVehicles} stats={stats} />
        </VC.InfoSection>
    );
};
