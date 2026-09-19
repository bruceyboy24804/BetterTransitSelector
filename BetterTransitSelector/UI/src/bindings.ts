import { OneWayBinding } from "utils/onewayBinding";
import triggers from "utils/trigger";
import type { AssetInfo, LineSummary, PackInfo, StatOptions, VehicleStats } from "./vehicle-stats";

// Keys must match BTS_VehicleStatsSystem's CreateBinding calls. OneWayBinding adds the "BINDING:"
// prefix and uses mod.json's id as the group, which is the same pair the C# side derives from
// Mod.Id -- a mismatch on either half fails silently, connecting nothing.
export const VEHICLE_STATS = new OneWayBinding<VehicleStats[]>("vehicleStats", []);

// Which stats the rows should show. Its own binding because it changes when the player touches a
// checkbox, where the stats table only changes on load.
export const STAT_OPTIONS = new OneWayBinding<StatOptions>("statOptions", {
    maxSpeed: true,
    passengers: true,
    acceleration: false,
    braking: false,
    energyType: false,
    carriages: false,
    length: false,
    sorting: "Default",
    listWidth: 0,
    listHeight: 0,
    favouritesWidth: 0,
    favouritesHeight: 0,
    speedUnit: "Auto",
    packPages: false,
    vehiclePages: false,
});

// The pack page for whichever upload was last requested. Requested by group id; the C# side
// answers with the cache entry, or valid: false when there is none.
export const PACK_INFO = new OneWayBinding<PackInfo>("packInfo", {
    valid: false,
    group: "",
    id: "",
    title: "",
    author: "",
    created: "",
    updated: "",
    version: "",
    shortDescription: "",
    longDescription: "",
    rating: 0,
    ratingsTotal: 0,
    subscriptions: 0,
    size: 0,
    cover: "",
    forumLink: "",
    screenshots: [],
    links: [],
    dependencies: [],
});
export const requestPackInfo = triggers.create<[string]>("requestPackInfo");

// The vehicle page, one tier down: requested by prefab name (a row's id), answered with the
// asset's file metadata and its consist car by car.
export const ASSET_INFO = new OneWayBinding<AssetInfo>("assetInfo", {
    valid: false,
    id: "",
    fileName: "",
    size: 0,
    created: "",
    modified: "",
    own: false,
    cars: [],
});
export const requestAssetInfo = triggers.create<[string]>("requestAssetInfo");

// Opens a link in the system browser, via C# -- a page's links have nowhere else to go.
export const openUrl = triggers.create<[string]>("openUrl");

// The selected line's fleet figures, re-read each frame on the C# side.
export const LINE_SUMMARY = new OneWayBinding<LineSummary>("lineSummary", {
    valid: false,
    vehicleCount: 0,
    targetCount: 0,
    intervalSeconds: 0,
    durationSeconds: 0,
    riders: 0,
    capacityNow: 0,
    platformLength: 0,
});

// The sort order, chosen in the filter panel; persisted in the settings and read back through
// STAT_OPTIONS.sorting. Values are Setting.SortOrder enum names.
export const setSorting = triggers.create<[string]>("setSorting");

// Starred vehicles, by prefab name -- the same string vanilla sends as a row's `id`, so the join
// is a Set lookup. Persisted per player on the C# side; the trigger flips one and the binding
// re-emits the whole set.
export const FAVOURITES = new OneWayBinding<string[]>("favourites", []);
export const toggleFavourite = triggers.create<[string]>("toggleFavourite");

// Starred packs, by group id (the upload's "Mod:<id>" name, which is what `groupOf` returns).
export const FAVOURITE_PACKS = new OneWayBinding<string[]>("favouritePacks", []);
export const toggleFavouritePack = triggers.create<[string]>("toggleFavouritePack");

// The last three picked, newest first. Reported on select only.
export const RECENT = new OneWayBinding<string[]>("recent", []);
export const recordUsed = triggers.create<[string]>("recordUsed");
