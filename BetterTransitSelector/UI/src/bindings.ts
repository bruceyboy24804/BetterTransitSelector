import { OneWayBinding } from "utils/onewayBinding";
import triggers from "utils/trigger";
import type { AssetInfo, ConsistBuilder, Entity, LineSummary, PackInfo, StatOptions, VehicleStats } from "./vehicle-stats";

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

export const CONSIST_BUILDER = new OneWayBinding<ConsistBuilder>("consistBuilder", {
    valid: false,
    lineName: "",
    status: "Vanilla",
    mismatched: 0,
    refitting: false,
    fixPrimary: { index: 0, version: 0 },
    fixSecondary: { index: 0, version: 0 },
    cars: [],
    related: [],
    models: [],
    catalogue: [],
});
// Blocks are (start, count) positions in the built train; a single car is a block of one.
export const consistInsert = triggers.create<[Entity, number]>("consistInsert");
export const consistRepeat = triggers.create<[number, number, number]>("consistRepeat");
export const consistRemove = triggers.create<[number, number]>("consistRemove");
export const consistMove = triggers.create<[number, number, number]>("consistMove");
export const consistFlip = triggers.create<[number, number]>("consistFlip");
// (start, count, "maxSpeed" | "acceleration" | "braking", value); speed in km/h, 0 resets to the prefab's.
export const consistSetStat = triggers.create<[number, number, string, number]>("consistSetStat");
export const consistTurnAll =triggers.create<[]>("consistTurnAll");
export const consistMirror = triggers.create<[]>("consistMirror");
export const consistLoadRecipe = triggers.create<[Entity]>("consistLoadRecipe");
export const consistClear = triggers.create<[]>("consistClear");
export const consistCopyRunning = triggers.create<[]>("consistCopyRunning");
export const consistRefit = triggers.create<[]>("consistRefit");
export const consistRefitStop = triggers.create<[]>("consistRefitStop");

// The 3D view: a live image of the built train, rendered while it is shown.
export const CONSIST_PREVIEW_URL = new OneWayBinding<string>("consistPreviewUrl", "");
export const consistPreviewShow = triggers.create<[boolean]>("consistPreviewShow");
export const consistPreviewOrbit = triggers.create<[number, number]>("consistPreviewOrbit");
export const consistPreviewZoom = triggers.create<[number]>("consistPreviewZoom");
// (start, count) of the cars to frame; count 0 frames the whole train.
export const consistPreviewFocus = triggers.create<[number, number]>("consistPreviewFocus");
export const consistPreviewReset = triggers.create<[]>("consistPreviewReset");
// Drag across the picture as a fraction of its width: slides the view along the train.
export const consistPreviewPan = triggers.create<[number]>("consistPreviewPan");
// [x0, y0, x1, y1] per car, then [x, y] per coupling (gap g in front of car g; the last is the tail), in 0..1.
// Pantographs up while running (CS2 trains have no wheel bones to turn).
export const CONSIST_PREVIEW_RUNNING = new OneWayBinding<boolean>("consistPreviewRunning", false);
export const consistPreviewRun = triggers.create<[boolean]>("consistPreviewRun");
// Dragging cars by hand: (start, count, x, y) with the pointer in 0..1 of the picture; the game lifts the block,
// parts the others and publishes the gap it would drop into (a consistMove "to"), -1 when not dragging.
export const consistPreviewDrag = triggers.create<[number, number, number, number]>("consistPreviewDrag");
export const consistPreviewDragEnd = triggers.create<[]>("consistPreviewDragEnd");
// (hover, selection start, selection count): the game tints those cars in 3D. -1 / 0 for none.
export const consistPreviewHighlight = triggers.create<[number, number, number]>("consistPreviewHighlight");
export const CONSIST_PREVIEW_DROP_GAP = new OneWayBinding<number>("consistPreviewDropGap", -1);
export const CONSIST_PREVIEW_LAYOUT = new OneWayBinding<number[]>("consistPreviewLayout", []);
