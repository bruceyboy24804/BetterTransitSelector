import { trigger } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { VC } from "vanilla/Components";
import { customOriginalOf, useVehicleName, useVehicleStats, type VehiclePrefab } from "../vehicle-stats";
import { recordUsed } from "../bindings";
import { VehicleDropdown } from "./vehicle-dropdown";
import { LineSummaryRow } from "./line-summary";
import { ConsistBuilderRow } from "./consist-builder";
import { CustomTrainsGroup } from "./custom-trains-group";

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

    // Custom cars (players' copies of a car with changed figures) are made and placed in the train builder.
    // They are hidden here unless selected, so a selection is never invisible.
    const visible = (available: VehiclePrefab[], selected: VehiclePrefab[] | null) =>
        available.filter(
            (v) => customOriginalOf(v.id) === null || (selected ?? []).some((s) => s.id === v.id),
        );
    const availablePrimary = visible(availablePrimaryVehicles, selectedPrimaryVehicles);
    const availableSecondary = availableSecondaryVehicles && visible(availableSecondaryVehicles, selectedSecondaryVehicles);

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
                available={availablePrimary}
                selected={selectedPrimaryVehicles}
                isPrimary={true}
                showSecondary={showSecondary}
                stats={stats}
                nameOf={nameOf}
                onToggle={toggle(true)}
                forceSearch={availableSecondaryVehicles !== null}
                renderHeader={
                    availableSecondaryVehicles
                        ? (query) => (
                              <CustomTrainsGroup
                                  query={query}
                                  selectedPrimary={selectedPrimaryVehicles}
                                  selectedSecondary={selectedSecondaryVehicles}
                              />
                          )
                        : undefined
                }
            />

            {showSecondary && availableSecondaryVehicles && selectedSecondaryVehicles && (
                <VehicleDropdown
                    label={label(LABEL_SECONDARY, "Select carriage model")}
                    available={availableSecondary ?? []}
                    selected={selectedSecondaryVehicles}
                    isPrimary={false}
                    showSecondary={showSecondary}
                    stats={stats}
                    nameOf={nameOf}
                    onToggle={toggle(false)}
                />
            )}

            <LineSummaryRow selected={selectedPrimaryVehicles} stats={stats} />

            {/* Rail lines only: vanilla sends the secondary lists as null for every other type. */}
            {availableSecondaryVehicles && <ConsistBuilderRow />}
        </VC.InfoSection>
    );
};
