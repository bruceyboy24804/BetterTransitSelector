import { trigger } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { VC } from "vanilla/Components";
import { useVehicleName, useVehicleStats, type VehiclePrefab } from "../vehicle-stats";
import { recordUsed } from "../bindings";
import { VehicleDropdown } from "./vehicle-dropdown";
import { LineSummaryRow } from "./line-summary";

/**
 * Vanilla's binding group and trigger names. Writes go through these rather than through the
 * VehicleModel buffer directly: the buffer edit and the panel refresh are a pair on the C# side,
 * and duplicating them is how selection state and panel state drift apart.
 */
const VANILLA_GROUP = "SelectVehiclesSection";
const SELECT = "selectVehicles";
const DESELECT = "deselectVehicles";

/** Entity.Null. The triggers take a primary and a secondary, so the unused half is passed as this. */
const NULL_ENTITY = { index: 0, version: 0 };

/**
 * Header strings. Both are hash-keyed localization ids -- the id is `<path>[<hash>]` and the hash is
 * the route prefab -- which is why the same section reads "Select car model" on a cargo train and
 * "Select carriage model" on a passenger one. Hardcoding either wording would be wrong on most
 * lines, so the route prefab does the work.
 */
const LABEL_PRIMARY = "SelectedInfoPanel.SELECT_VEHICLE_PRIMARY";
const LABEL_SECONDARY = "SelectedInfoPanel.SELECT_VEHICLE_SECONDARY";

export interface SelectVehiclesSectionProps {
    routePrefab: string;
    selectedPrimaryVehicles: VehiclePrefab[];
    /** Null -- not empty -- for every transport type except Train, Tram and Subway. */
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

    // Vanilla's rule for whether the secondary list applies at all, and it gates TWO things: the
    // whole secondary list, and the multi-unit marker on each row.
    //
    // A multi-unit prefab is a complete trainset -- engine and carriages in one -- so it needs no
    // separate locomotive. When every selected primary is multi-unit this is false and the engine
    // picker does not belong on the line. Gating only the marker and not the list is what put cargo
    // engines on a passenger line.
    const showSecondary =
        (availableSecondaryVehicles?.length ?? 0) > 0 &&
        selectedPrimaryVehicles.some((v) => !v.multiunit);

    const toggle = (isPrimary: boolean) => (vehicle: VehiclePrefab, select: boolean) => {
        const primary = isPrimary ? vehicle.entity : NULL_ENTITY;
        const secondary = isPrimary ? NULL_ENTITY : vehicle.entity;
        trigger(VANILLA_GROUP, select ? SELECT : DESELECT, primary, secondary);
        // Selecting is "using"; deselecting is not. Recorded by the same name the rows key on.
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

            {/* Under both pickers: what the primary mix means for the line. The primary is
                what carries passengers on every transport type; the secondary is engines. */}
            <LineSummaryRow selected={selectedPrimaryVehicles} stats={stats} />
        </VC.InfoSection>
    );
};
