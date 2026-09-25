import { OneWayBinding } from "utils/onewayBinding";
import triggers from "utils/trigger";
import type { AssetInfo, LineSummary, PackInfo, StatOptions, VehicleStats } from "./vehicle-stats";

export const VEHICLE_STATS = new OneWayBinding<VehicleStats[]>("vehicleStats", []);

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
    recentOpen: true,
    packPages: false,
    vehiclePages: false,
});

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

export const openUrl = triggers.create<[string]>("openUrl");

export const LINE_SUMMARY = new OneWayBinding<LineSummary>("lineSummary", {
    valid: false,
    vehicleCount: 0,
    targetCount: 0,
    intervalSeconds: 0,
    durationSeconds: 0,
    riders: 0,
    capacityNow: 0,
});

export const setSorting = triggers.create<[string]>("setSorting");

export const FAVOURITES = new OneWayBinding<string[]>("favourites", []);
export const toggleFavourite = triggers.create<[string]>("toggleFavourite");

export const FAVOURITE_PACKS = new OneWayBinding<string[]>("favouritePacks", []);
export const toggleFavouritePack = triggers.create<[string]>("toggleFavouritePack");

export const setRecentOpen = triggers.create<[boolean]>("setRecentOpen");

export const RECENT = new OneWayBinding<string[]>("recent", []);
export const recordUsed = triggers.create<[string]>("recordUsed");
