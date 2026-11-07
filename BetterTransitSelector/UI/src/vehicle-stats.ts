import { useMemo } from "react";
import { useValue } from "cs2/api";

import { useLocalization } from "cs2/l10n";
import { ASSET_INFO, CONSIST_BUILDER, FAVOURITES, FAVOURITE_PACKS, LINE_SUMMARY, PACK_INFO, RECENT, STAT_OPTIONS, VEHICLE_STATS } from "./bindings";

export interface Entity {
    index: number;
    version: number;
}

export interface VehicleStats {
    entity: Entity;

    maxSpeed: number;

    passengers: number;

    group: string;

    groupTitle: string;

    family: string;

    familyTitle: string;

    familyIcon: string;

    groupIcon: string;

    index: number;

    acceleration: number;

    braking: number;

    energyType: string;

    carriages: number;

    units: number;

    length: number;

    packageDate: string;

    theme: string;

    author: string;

    countries: string;

    icon: string;

    collection: string;

    collectionTitle: string;

    collectionIcon: string;

    collectionLocaleId: string;
}

export interface VehicleFilters {
    themes: string[];
    energies: string[];
    authors: string[];

    minPassengers: number;

    minSpeed: number;

    sources: string[];

    countries: string[];
}

export const NO_FILTERS: VehicleFilters = {
    themes: [],
    energies: [],
    authors: [],
    minPassengers: 0,
    minSpeed: 0,
    sources: [],
    countries: [],
};

export const countriesOf = (stats: VehicleStats | undefined): string[] =>
    stats && stats.countries ? stats.countries.split(",") : [];

export const flagUrl = (iso: string): string => `coui://bettertransitselector/flags/${iso}.svg`;

const COUNTRY_NAMES: Record<string, string> = {
    DE: "Germany", FR: "France", IT: "Italy", ES: "Spain", GB: "United Kingdom", NL: "Netherlands",
    BE: "Belgium", CH: "Switzerland", AT: "Austria", PL: "Poland", CZ: "Czechia", SE: "Sweden",
    NO: "Norway", DK: "Denmark", FI: "Finland", PT: "Portugal", HU: "Hungary", SK: "Slovakia",
    RO: "Romania", IE: "Ireland", LU: "Luxembourg", HR: "Croatia", SI: "Slovenia", GR: "Greece",
    US: "United States", CA: "Canada", JP: "Japan", CN: "China", KR: "South Korea", IN: "India",
    RU: "Russia", AU: "Australia",
};

export const useCountryName = (): ((iso: string) => string) => {
    const { translate } = useLocalization();
    return (iso) => translate(`BetterTransitSelector.Country[${iso}]`, COUNTRY_NAMES[iso] ?? iso) ?? iso;
};

export const SOURCE_VANILLA = "Vanilla";
export const SOURCE_MODDED = "Modded";

export const sourceOf = (stats: VehicleStats | undefined): string =>
    stats && stats.group ? SOURCE_MODDED : SOURCE_VANILLA;

export const filtersActive = (f: VehicleFilters): boolean =>
    f.themes.length > 0 ||
    f.energies.length > 0 ||
    f.authors.length > 0 ||
    f.minPassengers > 0 ||
    f.minSpeed > 0 ||
    f.sources.length > 0 ||
    f.countries.length > 0;

export const passesFilters = (
    stats: VehicleStats | undefined,
    filters: VehicleFilters,
): boolean => {
    if (!filtersActive(filters)) return true;
    if (!stats) return false;

    if (filters.sources.length > 0 && !filters.sources.includes(sourceOf(stats))) return false;
    if (filters.countries.length > 0) {
        const mine = countriesOf(stats);
        if (!filters.countries.some((c) => mine.includes(c))) return false;
    }
    if (filters.themes.length > 0 && !filters.themes.includes(stats.theme)) return false;
    if (filters.authors.length > 0 && !filters.authors.includes(stats.author)) return false;

    if (filters.energies.length > 0) {
        const mine = stats.energyType.split(",").map((e) => e.trim());
        if (!filters.energies.some((e) => mine.includes(e))) return false;
    }

    if (filters.minPassengers > 0 && stats.passengers < filters.minPassengers) return false;
    if (filters.minSpeed > 0 && stats.maxSpeed < filters.minSpeed) return false;

    return true;
};

export interface StatOptions {
    maxSpeed: boolean;
    passengers: boolean;
    acceleration: boolean;
    braking: boolean;
    energyType: boolean;
    carriages: boolean;
    length: boolean;

    sorting: string;

    listWidth: number;

    listHeight: number;

    favouritesWidth: number;

    favouritesHeight: number;

    speedUnit: string;

    recentOpen: boolean;

    packPages: boolean;

    vehiclePages: boolean;
}

export interface PackInfo {
    valid: boolean;
    group: string;
    id: string;
    title: string;
    author: string;

    created: string;
    updated: string;
    version: string;
    shortDescription: string;

    longDescription: string;

    rating: number;

    ratingsTotal: number;
    subscriptions: number;

    size: number;

    cover: string;
    forumLink: string;
    screenshots: string[];
    links: { type: string; url: string }[];
    dependencies: { id: string; name: string; installed: boolean }[];
}

export const usePackInfo = (): PackInfo => useValue(PACK_INFO.binding);

export interface AssetCar {
    id: string;

    count: number;
    passengers: number;

    length: number;
}

export interface AssetInfo {
    valid: boolean;
    id: string;

    fileName: string;

    size: number;

    created: string;
    modified: string;

    own: boolean;

    cars: AssetCar[];
}

export const carriagesText = (stats: VehicleStats): string =>
    stats.units > 1
        ? `${stats.carriages} (${stats.units} x ${Math.round(stats.carriages / stats.units)})`
        : `${stats.carriages}`;

export const useAssetInfo = (): AssetInfo => useValue(ASSET_INFO.binding);

export const useStatOptions = (): StatOptions => useValue(STAT_OPTIONS.binding);

export interface LineSummary {
    valid: boolean;

    vehicleCount: number;

    targetCount: number;
    intervalSeconds: number;
    durationSeconds: number;

    riders: number;

    capacityNow: number;
}

export const projectedLoad = (line: LineSummary, passengers: number): number | null =>
    line.valid && line.targetCount > 0 && passengers > 0 ? line.riders / (line.targetCount * passengers) : null;

export const TARGET_LOAD = 0.8;

export const vehiclesNeeded = (line: LineSummary, passengers: number): number =>
    passengers > 0 ? Math.max(1, Math.ceil(line.riders / (TARGET_LOAD * passengers))) : 0;

export const useLineSummary = (): LineSummary => useValue(LINE_SUMMARY.binding);

export const useFavourites = (): Set<string> => {
    const names = useValue(FAVOURITES.binding);
    return useMemo(() => new Set(names), [names]);
};

export const useFavouritePacks = (): Set<string> => {
    const ids = useValue(FAVOURITE_PACKS.binding);
    return useMemo(() => new Set(ids), [ids]);
};

export const useRecent = (): string[] => useValue(RECENT.binding);

// A car prefab's figures as the simulation reads them. Speed in km/h, acceleration and braking in m/s².
export interface CarFigures {
    passengers: number;
    cargo: number;
    maxSpeed: number;
    acceleration: number;
    braking: number;
}

export type CarStat = keyof CarFigures;

export interface ConsistSlot {
    prefab: Entity;
    id: string;
    reversed: boolean;

    missing: boolean;

    lead: boolean;

    // For a custom car, its original asset's prefab name; "" for an asset's own car.
    customOf: string;

    kind: string;
    length: number;
    thumbnail: string;

    figures: CarFigures;

    // The original asset's figures; equal to `figures` unless this is a custom car.
    baseFigures: CarFigures;
}

export interface ConsistCatalogueCar {
    entity: Entity;
    id: string;

    kind: string;

    length: number;
    thumbnail: string;

    pack: string;

    author: string;

    figures: CarFigures;
}

// Custom cars are prefabs named "<original>#BTS:<hash>" (CustomCar.kSeparator on the C# side).
const CUSTOM_SEPARATOR = "#BTS:";

/** The original asset's prefab name when `prefabName` is a custom car's, else null. */
export const customOriginalOf = (prefabName: string): string | null => {
    const at = prefabName.indexOf(CUSTOM_SEPARATOR);
    return at > 0 ? prefabName.slice(0, at) : null;
};

export interface ConsistConfigRow {
    id: string;
    name: string;
    lead: Entity;
    engine: Entity;
    thumbnail: string;
    cars: number;
    passengers: number;
    cargo: number;
    maxSpeed: number;
    missing: boolean;
    canLead: boolean;
    selected: boolean;
}

export interface ConsistBuilder {
    valid: boolean;

    lineName: string;

    status: string;

    // Running trains that differ from the built train; a rolling refit replaces them one at a time.
    mismatched: number;
    refitting: boolean;
    editingId: string;
    editingName: string;
    // What the line runs: the game's trains, a saved train, or a train no saved one matches.
    lineState: "Default" | "Config" | "Modified";
    otherModels: boolean;
    configs: ConsistConfigRow[];
    cars: ConsistSlot[];

    related: Entity[];

    models: Entity[];
    catalogue: ConsistCatalogueCar[];
}

export const useConsistBuilder = (): ConsistBuilder => useValue(CONSIST_BUILDER.binding);

export interface VehiclePrefab {
    entity: Entity;

    id: string;
    locked: boolean;
    multiunit: boolean;
    requirements: unknown;
    thumbnail: string;
    objectRequirementIcons: string[] | null;
}

export const thumbnailOf = (vehicle: VehiclePrefab, stats: StatsLookup): string =>
    stats(vehicle)?.icon || vehicle.thumbnail;

export const entityKey = (entity: Entity): string => `${entity.index}:${entity.version}`;

export type StatsLookup = (vehicle: VehiclePrefab) => VehicleStats | undefined;

export const useVehicleStats = (): StatsLookup => {
    const stats = useValue(VEHICLE_STATS.binding);

    const byEntity = useMemo(() => {
        const map = new Map<string, VehicleStats>();
        for (const entry of stats) {
            map.set(entityKey(entry.entity), entry);
        }
        return map;
    }, [stats]);

    return (vehicle: VehiclePrefab) => byEntity.get(entityKey(vehicle.entity));
};

export const useVehicleName = (): ((vehicle: VehiclePrefab) => string) => {
    const { nameOf } = useAssetText();
    return (vehicle: VehiclePrefab) => nameOf(vehicle.id);
};

export const useT = (): ((key: string, fallback: string, vars?: Record<string, string | number>) => string) => {
    const { translate } = useLocalization();

    return (key, fallback, vars) => {
        let text = translate(`BetterTransitSelector.UI[${key}]`, fallback) ?? fallback;
        if (vars) {
            for (const [name, value] of Object.entries(vars)) {
                text = text.split(`{${name}}`).join(String(value));
            }
        }
        return text;
    };
};

export const useAssetText = (): {
    nameOf: (prefabName: string) => string;
    descriptionOf: (prefabName: string) => string | null;
    headingOf: (title: string, localeId: string) => string;
} => {
    const { translate } = useLocalization();
    const some = (v: string | null | undefined) => (v && v.length > 0 ? v : null);
    const assetName = (prefabName: string) => translate(`Assets.NAME[${prefabName}]`, prefabName) ?? prefabName;
    const custom = translate("BetterTransitSelector.UI[Consist.CustomSuffix]", "custom") ?? "custom";

    return {
        // A custom car has no translation of its own: it goes by its asset's name, marked as custom.
        nameOf: (prefabName) => {
            const original = customOriginalOf(prefabName);
            return original ? `${assetName(original)} (${custom})` : assetName(prefabName);
        },
        descriptionOf: (prefabName) => some(translate(`Assets.DESCRIPTION[${prefabName}]`, null)),
        // A display name is typed text, so a translation is usually keyed by the parent pack's prefab name.
        headingOf: (title, localeId) =>
            some(translate(`Assets.NAME[${title}]`, null)) ??
            (localeId ? some(translate(`Assets.NAME[${localeId}]`, null)) : null) ??
            title,
    };
};

export const groupOf = (stats: VehicleStats | undefined): string | null =>
    stats && stats.group.length > 0 ? stats.group : null;

export const folderOf = (stats: VehicleStats | undefined): string | null =>
    stats && stats.collection ? stats.collection : groupOf(stats);

export const isCollection = (folder: string): boolean => !folder.startsWith("Mod:");

export const familyOf = (displayName: string): string => {
    const token = displayName.trim().split(/[\s(]+/)[0] ?? "";
    return token.toUpperCase();
};

export const useGroupName = (): ((
    group: string,
    parentAsset: string,
    firstMember: string,
) => string) => {
    const { translate } = useLocalization();

    const some = (v: string | null | undefined) => (v && v.length > 0 ? v : null);
    const localize = (id: string) => (some(id) ? some(translate(`Assets.NAME[${id}]`, null)) : null);

    return (group: string, parentAsset: string, firstMember: string) =>
        localize(parentAsset) ??
        some(parentAsset) ??
        localize(group) ??
        some(firstMember) ??
        group;
};

export const comparatorFor = (
    sorting: string,
    stats: StatsLookup,
    nameOf: (vehicle: VehiclePrefab) => string,
): ((a: VehiclePrefab, b: VehiclePrefab) => number) | null => {
    if (sorting === "Name") {
        return (a, b) => nameOf(a).localeCompare(nameOf(b));
    }

    if (sorting === "Newest" || sorting === "Oldest") {
        const direction = sorting === "Newest" ? -1 : 1;
        return (a, b) => {
            const da = stats(a)?.packageDate ?? "";
            const db = stats(b)?.packageDate ?? "";
            if (da === db) return nameOf(a).localeCompare(nameOf(b));
            if (da === "") return 1;
            if (db === "") return -1;
            return da < db ? -direction : direction;
        };
    }

    const byStat = (pick: (s: VehicleStats) => number) => (a: VehiclePrefab, b: VehiclePrefab) => {
        const sa = stats(a);
        const sb = stats(b);
        const va = sa ? pick(sa) : 0;
        const vb = sb ? pick(sb) : 0;
        if (va === vb) return nameOf(a).localeCompare(nameOf(b));
        if (va === 0) return 1;
        if (vb === 0) return -1;
        return vb - va;
    };
    if (sorting === "Speed") return byStat((s) => s.maxSpeed);
    if (sorting === "Passengers") return byStat((s) => s.passengers);
    if (sorting === "Length") return byStat((s) => s.length);

    return null;
};
