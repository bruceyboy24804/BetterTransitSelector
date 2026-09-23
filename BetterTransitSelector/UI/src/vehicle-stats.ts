import { useMemo } from "react";
import { useValue } from "cs2/api";
// useLocalization, NOT useCachedLocalization. The .d.ts declares the latter internally and
// re-exports it under the former name; importing the internal name type-checks but is not in the
// runtime export map, so it fails at render with "is not a function".
import { useLocalization } from "cs2/l10n";
import { ASSET_INFO, FAVOURITES, FAVOURITE_PACKS, LINE_SUMMARY, PACK_INFO, RECENT, STAT_OPTIONS, VEHICLE_STATS } from "./bindings";

/** An ECS entity as the bindings encode it. */
export interface Entity {
    index: number;
    version: number;
}

/**
 * One vehicle prefab's stats, as written by BTS_VehicleStatsSystem. Property names must match
 * VehicleStats.Write on the C# side.
 */
export interface VehicleStats {
    entity: Entity;
    /** Top speed in km/h, already converted and rounded. 0 when unknown. */
    maxSpeed: number;
    /** Passenger capacity. 0 when the prefab carries no passenger data. */
    passengers: number;
    /**
     * The package this vehicle was published in, by prefab name; "" when it has none.
     *
     * The game's own packaging relation: everything from one upload shares a content prerequisite
     * prefab named "Mod:<pdxId>". One package per vehicle, so no rule is needed for picking between
     * several.
     */
    group: string;
    /**
     * The package's parent asset, per REVO's rule that the headline comes from it; "" when the
     * package ships none. Loadout-eligible assets are the children, so the parent is never one of
     * the entries vanilla sends us -- it has to come across on this binding.
     */
    groupTitle: string;
    /**
     * The placeholder this train was declared a variant of, by asset GUID; "" when none. A tier
     * INSIDE the group, never a replacement for it: the upload is the group, this is a family
     * within it, and `index` orders the family.
     */
    family: string;
    /** The placeholder's name, the family's heading; "" with no family. */
    familyTitle: string;
    /** The pack prefab's own icon (its UIObject component), for the family header; "" when none. */
    familyIcon: string;
    /** The upload's icon, from its single iconed pack prefab; "" when none or ambiguous. */
    groupIcon: string;
    /**
     * Sort order within a family, low first; Int32.MaxValue when the author set none. An index
     * rather than a list position, so a livery uploaded later can slot into an existing family
     * without anything being republished.
     */
    index: number;
    /** Acceleration in m/s²; 0 when the prefab carries none. */
    acceleration: number;
    /** Braking in m/s²; 0 when the prefab carries none. */
    braking: number;
    /** What it runs on, e.g. "Electricity"; "" when unknown. Flags, so dual-mode lists both. */
    energyType: string;
    /** Passenger-carrying cars in the WHOLE consist, every unit counted; 0 when unknown. */
    carriages: number;
    /** Units a multi-unit train runs as (a 2x3 is 2); 1 for everything else. */
    units: number;
    /** Length in whole metres; 0 when unknown. */
    length: number;
    /** Publication date of the upload, ISO 8601 UTC; "" when unknown. Sorts as a string. */
    packageDate: string;
    /** Theme, e.g. "EU"; "" when theme-agnostic. */
    theme: string;
    /** Creator who published the upload; "" when unknown. */
    author: string;
    /** Countries the creator declared, comma-separated ISO codes; "" when none. Also the flag file names. */
    countries: string;
    /** A picker-only icon from the variant component; "" means the vehicle's own thumbnail. */
    icon: string;
    /** Folder key above the family -- "Col:<guid>" or "Heading:<name>" -- when it has one; "" for the upload. */
    collection: string;
    /** The folder's heading: a display name as typed, or a parent pack's prefab name (localised via Assets.NAME when it is one). */
    collectionTitle: string;
    /** The folder's icon; "" when none. */
    collectionIcon: string;
}

/**
 * The active filters. Empty sets mean "no restriction" rather than "exclude everything", so the
 * default state shows the whole list.
 */
export interface VehicleFilters {
    themes: string[];
    energies: string[];
    authors: string[];
    /** Minimum passenger capacity; 0 for no minimum. */
    minPassengers: number;
    /** Minimum top speed in km/h; 0 for no minimum. */
    minSpeed: number;
    /** "Vanilla" and/or "Modded"; empty for both. A vehicle is vanilla when it has no upload. */
    sources: string[];
    /** ISO codes; a vehicle passes when it declares any of them. */
    countries: string[];
    /** Hide vehicles longer than the line's shortest platform. No effect off rail lines. */
    fitsLine: boolean;
}

export const NO_FILTERS: VehicleFilters = {
    themes: [],
    energies: [],
    authors: [],
    minPassengers: 0,
    minSpeed: 0,
    sources: [],
    countries: [],
    fitsLine: false,
};

/** The declared countries as a list; [] when none. */
export const countriesOf = (stats: VehicleStats | undefined): string[] =>
    stats && stats.countries ? stats.countries.split(",") : [];

/** Flag image for an ISO code, from the mod's own hosted assets. */
export const flagUrl = (iso: string): string => `coui://bettertransitselector/flags/${iso}.svg`;

/** English country names by ISO code, for tooltips; the code itself is the fallback. */
const COUNTRY_NAMES: Record<string, string> = {
    DE: "Germany", FR: "France", IT: "Italy", ES: "Spain", GB: "United Kingdom", NL: "Netherlands",
    BE: "Belgium", CH: "Switzerland", AT: "Austria", PL: "Poland", CZ: "Czechia", SE: "Sweden",
    NO: "Norway", DK: "Denmark", FI: "Finland", PT: "Portugal", HU: "Hungary", SK: "Slovakia",
    RO: "Romania", IE: "Ireland", LU: "Luxembourg", HR: "Croatia", SI: "Slovenia", GR: "Greece",
    US: "United States", CA: "Canada", JP: "Japan", CN: "China", KR: "South Korea", IN: "India",
    RU: "Russia", AU: "Australia",
};

/** Localised country name for an ISO code: `BetterTransitSelector.Country[XX]`, English fallback. */
export const useCountryName = (): ((iso: string) => string) => {
    const { translate } = useLocalization();
    return (iso) => translate(`BetterTransitSelector.Country[${iso}]`, COUNTRY_NAMES[iso] ?? iso) ?? iso;
};

export const SOURCE_VANILLA = "Vanilla";
export const SOURCE_MODDED = "Modded";

/** Vanilla means shipped with the game: no upload behind it, so no package and no author. */
export const sourceOf = (stats: VehicleStats | undefined): string =>
    stats && stats.group ? SOURCE_MODDED : SOURCE_VANILLA;

/** Whether any filter is actually narrowing the list. */
export const filtersActive = (f: VehicleFilters): boolean =>
    f.themes.length > 0 ||
    f.energies.length > 0 ||
    f.authors.length > 0 ||
    f.minPassengers > 0 ||
    f.minSpeed > 0 ||
    f.sources.length > 0 ||
    f.countries.length > 0 ||
    f.fitsLine;

/**
 * Whether a vehicle passes the filters.
 *
 * A vehicle with no value for a filtered field fails that filter: asking for European stock should
 * not hand back everything theme-agnostic as well. The numeric minimums treat a missing value as 0,
 * so they exclude anything whose figure is unknown -- which is the same rule and stays predictable.
 */
export const passesFilters = (
    stats: VehicleStats | undefined,
    filters: VehicleFilters,
    line?: LineSummary,
): boolean => {
    if (!filtersActive(filters)) return true;
    if (!stats) return false;

    if (filters.fitsLine && line && overhangs(line, stats)) return false;
    if (filters.sources.length > 0 && !filters.sources.includes(sourceOf(stats))) return false;
    if (filters.countries.length > 0) {
        const mine = countriesOf(stats);
        if (!filters.countries.some((c) => mine.includes(c))) return false;
    }
    if (filters.themes.length > 0 && !filters.themes.includes(stats.theme)) return false;
    if (filters.authors.length > 0 && !filters.authors.includes(stats.author)) return false;

    // energyType is a flags string, so "Diesel, Electricity" must match a request for either.
    if (filters.energies.length > 0) {
        const mine = stats.energyType.split(",").map((e) => e.trim());
        if (!filters.energies.some((e) => mine.includes(e))) return false;
    }

    if (filters.minPassengers > 0 && stats.passengers < filters.minPassengers) return false;
    if (filters.minSpeed > 0 && stats.maxSpeed < filters.minSpeed) return false;

    return true;
};

/** Which stats the rows should show, mirrored from the mod's settings. */
export interface StatOptions {
    maxSpeed: boolean;
    passengers: boolean;
    acceleration: boolean;
    braking: boolean;
    energyType: boolean;
    carriages: boolean;
    length: boolean;
    /** "Default" | "Name" | "Newest" | "Oldest" -- the enum name from the settings. */
    sorting: string;
    /** Minimum width of the open list in rem; 0 means fit to contents. */
    listWidth: number;
    /** Height of the list's scroll box in rem; 0 means the default. */
    listHeight: number;
    /** Favourites panel width in rem; 0 means the default. */
    favouritesWidth: number;
    /** Favourites scroll box height in rem; 0 means the default. */
    favouritesHeight: number;
    /** "Auto" | "Kph" | "Mph" -- Auto follows the game's unit system. */
    speedUnit: string;
    /** Whether the Recently used block at the top of the list is expanded. */
    recentOpen: boolean;
    /** Whether pack headers offer the info button that opens the upload's page. */
    packPages: boolean;
    /** Whether vehicle rows offer the info button that opens the vehicle's page. */
    vehiclePages: boolean;
}

/** An upload's page, from the game's own mod cache. Property names match PackInfo.Write. */
export interface PackInfo {
    valid: boolean;
    group: string;
    id: string;
    title: string;
    author: string;
    /** ISO 8601. */
    created: string;
    updated: string;
    version: string;
    shortDescription: string;
    /** Markdown, the PDX subset. */
    longDescription: string;
    /** Always ~5 on PDX; not a star rating. */
    rating: number;
    /** Likes -- a PDX rating is a thumbs-up. */
    ratingsTotal: number;
    subscriptions: number;
    /** Bytes. */
    size: number;
    /** CDN URL of the cover image; "" when none. */
    cover: string;
    forumLink: string;
    screenshots: string[];
    links: { type: string; url: string }[];
    dependencies: { id: string; name: string; installed: boolean }[];
}

export const usePackInfo = (): PackInfo => useValue(PACK_INFO.binding);

/** One car of a consist, as AssetInfo.Write emits it. */
export interface AssetCar {
    /** Prefab name; localised through the Assets.NAME table like any row. */
    id: string;
    /** How many of this car the consist runs at its longest. */
    count: number;
    passengers: number;
    /** Metres. */
    length: number;
}

/** A vehicle asset's page. Property names match AssetInfo.Write. */
export interface AssetInfo {
    valid: boolean;
    id: string;
    /** The asset file's name; "" for a vanilla vehicle, which has no file of its own. */
    fileName: string;
    /** Bytes. */
    size: number;
    /** ISO 8601; "" when unknown. */
    created: string;
    modified: string;
    /** The player's own editor-made asset, rather than a subscription. */
    own: boolean;
    /** Head car first, then each carriage entry. */
    cars: AssetCar[];
}

/**
 * The carriage figure as creators write it: "2 x 3" for two three-car units, "3" for one.
 * Maestro's ask -- a bare 3 on a 2x3 reads as a three-car train.
 */
export const carriagesText = (stats: VehicleStats): string =>
    stats.units > 1 ? `${stats.units} x ${Math.round(stats.carriages / stats.units)}` : `${stats.carriages}`;

export const useAssetInfo = (): AssetInfo => useValue(ASSET_INFO.binding);

/** Subscribes to the display options. Changes as soon as the player touches a checkbox. */
export const useStatOptions = (): StatOptions => useValue(STAT_OPTIONS.binding);

/** The selected line's fleet figures, as BTS_LineSummarySystem writes them. */
export interface LineSummary {
    /** False when the selection is not a transport line; the UI shows nothing then. */
    valid: boolean;
    /** Vehicles currently on the line. */
    vehicleCount: number;
    /** Vehicles the line wants: round(duration / interval), the game's own formula. */
    targetCount: number;
    intervalSeconds: number;
    durationSeconds: number;
    /** Passengers aboard the fleet right now -- the game's demand figure. */
    riders: number;
    /** Seats aboard the fleet right now. riders / capacityNow is the overview's "usage". */
    capacityNow: number;
    /** Shortest platform on the line in metres; 0 when unknown or not a rail line. */
    platformLength: number;
}

/**
 * Whether a vehicle overhangs the line's shortest platform. A realism flag, not a game rule --
 * the game lets it stop anyway -- so it is a warning, never a hidden row. False when either
 * length is unknown.
 */
export const overhangs = (line: LineSummary, stats: VehicleStats | undefined): boolean =>
    line.platformLength > 0 && !!stats && stats.length > 0 && stats.length > line.platformLength;

/**
 * The load a vehicle would run at on this line: today's riders spread over the fleet the line
 * wants, each vehicle seating `passengers`. Null when there is nothing to compute from.
 */
export const projectedLoad = (line: LineSummary, passengers: number): number | null =>
    line.valid && line.targetCount > 0 && passengers > 0 ? line.riders / (line.targetCount * passengers) : null;

/** Comfortable ceiling for load; above it the summary suggests more vehicles. */
export const TARGET_LOAD = 0.8;

/** Vehicles of `passengers` seats needed to keep today's riders under TARGET_LOAD. */
export const vehiclesNeeded = (line: LineSummary, passengers: number): number =>
    passengers > 0 ? Math.max(1, Math.ceil(line.riders / (TARGET_LOAD * passengers))) : 0;

export const useLineSummary = (): LineSummary => useValue(LINE_SUMMARY.binding);

/** The starred prefab names as a Set, so a row's check is one lookup on its `id`. */
export const useFavourites = (): Set<string> => {
    const names = useValue(FAVOURITES.binding);
    return useMemo(() => new Set(names), [names]);
};

/** The starred pack ids as a Set. */
export const useFavouritePacks = (): Set<string> => {
    const ids = useValue(FAVOURITE_PACKS.binding);
    return useMemo(() => new Set(ids), [ids]);
};

/** The last few picked prefab names, newest first. */
export const useRecent = (): string[] => useValue(RECENT.binding);

/**
 * One entry in vanilla's available/selected vehicle lists
 * (SelectVehiclesSection+VehiclePrefab). Note what is absent: no speed, no capacity, no group --
 * that gap is why the stats binding exists.
 */
export interface VehiclePrefab {
    entity: Entity;
    /** The raw prefab name. The localization hash for the displayed name, not the displayed name. */
    id: string;
    locked: boolean;
    multiunit: boolean;
    requirements: unknown;
    thumbnail: string;
    objectRequirementIcons: string[] | null;
}

/**
 * The picture to draw for a vehicle in the picker: the creator's picker-only icon when set on
 * the variant component, else the thumbnail vanilla sends. One place, so every row, pill and
 * group fallback agrees.
 */
export const thumbnailOf = (vehicle: VehiclePrefab, stats: StatsLookup): string =>
    stats(vehicle)?.icon || vehicle.thumbnail;

/**
 * Stable key for an entity. Index alone is not unique -- entities are recycled and the version
 * distinguishes generations -- so both halves are part of the key.
 */
export const entityKey = (entity: Entity): string => `${entity.index}:${entity.version}`;

/** Looks up the stats for a vehicle, or undefined when the table has no row for it. */
export type StatsLookup = (vehicle: VehiclePrefab) => VehicleStats | undefined;

/**
 * Subscribes to the stats table and returns a lookup keyed by entity.
 *
 * Built as a map rather than a scan because the selector renders one row per available vehicle and
 * a linear search per row is quadratic over a large modded list -- which is the exact situation
 * this mod exists for.
 */
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

/**
 * Resolves the name the player actually sees for a vehicle.
 *
 * Vanilla renders the row label through a localized asset-name component keyed by the prefab id, so
 * the displayed name is never in the props -- only the raw id is. Anything matching on what the
 * user reads (search, sorting by name) has to translate first, or it matches internal prefab names
 * instead of the visible ones. The id is the fallback because that is what vanilla shows when a
 * translation is missing, which is common for modded assets.
 */
export const useVehicleName = (): ((vehicle: VehiclePrefab) => string) => {
    const { translate } = useLocalization();

    return (vehicle: VehiclePrefab) =>
        translate(`Assets.NAME[${vehicle.id}]`, vehicle.id) ?? vehicle.id;
};

/**
 * The mod's own UI strings, from L10n/Locale.json (and its per-locale siblings), keyed
 * `BetterTransitSelector.UI[<key>]`. The English text is passed as the fallback so the UI still
 * reads correctly if a key is missing from a translation -- or from the file.
 *
 * `{NAME}` placeholders are substituted from `vars`; the game's own templates use the same
 * braces, so a translator sees a familiar shape.
 */
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

/**
 * The editor's per-asset localisation: a creator fills in Name and Description on any asset, per
 * language, and the game stores them as `Assets.NAME[<prefab>]` / `Assets.DESCRIPTION[<prefab>]`.
 * Name falls back to the raw prefab name; Description to null, since most assets have none and
 * there is nothing sensible to show in its place.
 */
export const useAssetText = (): {
    nameOf: (prefabName: string) => string;
    descriptionOf: (prefabName: string) => string | null;
} => {
    const { translate } = useLocalization();
    const some = (v: string | null | undefined) => (v && v.length > 0 ? v : null);

    return {
        nameOf: (prefabName) => translate(`Assets.NAME[${prefabName}]`, prefabName) ?? prefabName,
        descriptionOf: (prefabName) => some(translate(`Assets.DESCRIPTION[${prefabName}]`, null)),
    };
};

/**
 * The pack a vehicle is displayed under, or null when it belongs to none.
 *
 * Pack membership is many-to-many, so a display rule is needed and this is it: the first pack the
 * game reports. That is deterministic but arbitrary -- a repaint that its author put in both the
 * engine's family pack and its own will show under whichever the game lists first. Picking properly
 * (author intent, or a mod-owned override prefab) is the open half of goal #3.
 */
export const groupOf = (stats: VehicleStats | undefined): string | null =>
    stats && stats.group.length > 0 ? stats.group : null;

/**
 * The picker's top-level folder for a vehicle: its collection or shared heading when its family has one
 * ("Col:<guid>" for an exclusive parent pack, "Heading:<name>" for a name-keyed heading -- the C#
 * side chooses and prefixes), else its upload ("Mod:<id>"). A collection spans uploads -- that
 * is what it is for -- so this, not `groupOf`, is what the list groups by; `groupOf` stays the
 * upload for pack pages and the like.
 */
export const folderOf = (stats: VehicleStats | undefined): string | null =>
    stats && stats.collection ? stats.collection : groupOf(stats);

export const isCollection = (folder: string): boolean => !folder.startsWith("Mod:");

/**
 * The family a vehicle belongs to within its package: the leading token of its displayed name.
 *
 * Nothing in the game expresses this level. Asset packs are unused, UI groups are null, priorities
 * are all 1, and the published subPath is only the mod cache folder -- so the middle tier of the
 * listing the creators drew has to be derived, and the names are the only thing carrying it.
 *
 * In practice they carry it reliably, because the convention is already universal:
 *   "BR481 S-Bahn Berlin Half-Train (1x4 - 600)"  -> BR481
 *   "Br422 Generic S-Bahn Train (1x4 - 600)"      -> BR422
 * Model first, configuration after, which is the same shape as the mockup's "BR612 Config 1x2".
 *
 * Upper-cased because creators disagree on capitalisation ("BR422" against "Br422") and the two
 * should not become separate families. Returns "" when a name has no leading token to take.
 */
export const familyOf = (displayName: string): string => {
    const token = displayName.trim().split(/[\s(]+/)[0] ?? "";
    return token.toUpperCase();
};

/**
 * Resolves a group's headline.
 *
 * REVO's rule is that the headline comes from the package's parent asset, so that is tried first
 * and everything else is a fallback for packages that ship none:
 *
 *   1. The parent asset's name, localized -- what REVO specifies, and what the placeholder "train
 *      pack" prefab will supply once creators ship one.
 *   2. The package prefab's own localized name, if the game has one.
 *   3. The name of the first member, which reads far better than a bare "Mod:157217".
 *   4. The raw package name, so something always renders.
 *
 * Steps 3 and 4 are stand-ins, and they are the reason the placeholder prefab matters: only step 1
 * is the author's deliberate choice.
 */
export const useGroupName = (): ((
    group: string,
    parentAsset: string,
    firstMember: string,
) => string) => {
    const { translate } = useLocalization();

    // "" is how the C# side reports "absent", and ?? does not fall through an empty string, so
    // every step is normalised to null before the chain.
    const some = (v: string | null | undefined) => (v && v.length > 0 ? v : null);
    const localize = (id: string) => (some(id) ? some(translate(`Assets.NAME[${id}]`, null)) : null);

    return (group: string, parentAsset: string, firstMember: string) =>
        localize(parentAsset) ??
        some(parentAsset) ??
        localize(group) ??
        some(firstMember) ??
        group;
};

/**
 * Builds a comparator for the chosen sort order.
 *
 * Returns null for "Default", which is meaningful rather than lazy: it leaves the order the game
 * emitted untouched, so the mod imposes nothing the player did not ask for.
 *
 * Date sorting keys on the publication date of the upload, which every asset in a package shares.
 * That makes it a sort by pack rather than by individual vehicle -- the right granularity for "what
 * did I install recently" -- and ties are broken by name so the result is stable rather than
 * dependent on the order the game happened to emit.
 */
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
            // ISO 8601 UTC strings, so a plain comparison is a date comparison. Undated entries
            // sort last in both directions rather than pretending to be the oldest.
            const da = stats(a)?.packageDate ?? "";
            const db = stats(b)?.packageDate ?? "";
            if (da === db) return nameOf(a).localeCompare(nameOf(b));
            if (da === "") return 1;
            if (db === "") return -1;
            return da < db ? -direction : direction;
        };
    }

    // Stat sorts: highest first, since "fastest" and "biggest" are what the player is asking for.
    // Unknown (0) sorts last, as with dates; ties break by name for a stable result.
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
