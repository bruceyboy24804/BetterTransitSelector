import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { VC, VT } from "vanilla/Components";
import { c } from "utils/classes";
import {
    NO_FILTERS,
    SOURCE_VANILLA,
    sourceOf,
    countriesOf,
    thumbnailOf,
    comparatorFor,
    familyOf,
    groupOf,
    folderOf,
    isCollection,
    useGroupName,
    useAssetText,
    useT,
    filtersActive,
    passesFilters,
    useStatOptions,
    useFavourites,
    useFavouritePacks,
    useRecent,
    type StatsLookup,
    type VehicleFilters,
    type VehiclePrefab,
} from "../vehicle-stats";
import { requestAssetInfo, requestPackInfo, setRecentOpen as setRecentOpen$ } from "../bindings";
import { PackPage } from "./pack-page";
import { VehiclePage } from "./vehicle-page";
import { VehicleFiltersBar } from "./vehicle-filters";
import { VehicleGroup, type VehicleSubGroup } from "./vehicle-group";
import { VehicleRow } from "./vehicle-row";
import styles from "./vehicle-dropdown.module.scss";

/**
 * Below this many entries a search box is clutter -- the whole list is already on screen. Vanilla
 * lines routinely offer two or three vehicles; the modded lists this mod exists for run to dozens.
 */
const SEARCH_THRESHOLD = 6;

// The Thick* set is the only one shipping all four directions.
const ARROW_OPEN = "Media/Glyphs/ThickStrokeArrowRight.svg";
const CHEVRON_UP = "Media/Glyphs/ThickStrokeArrowUp.svg";
const CHEVRON_DOWN = "Media/Glyphs/ThickStrokeArrowDown.svg";
const ARROW_CLOSE = "Media/Glyphs/ThickStrokeArrowLeft.svg";

export interface VehicleDropdownProps {
    /** Already-translated header text, e.g. "Select carriage model". */
    label: string;
    available: VehiclePrefab[];
    selected: VehiclePrefab[];
    /** True for the primary list, false for the secondary (carriage/engine) one. */
    isPrimary: boolean;
    /** Vanilla's showSecondary: gates the multi-unit marker on each row. */
    showSecondary: boolean;
    stats: StatsLookup;
    nameOf: (vehicle: VehiclePrefab) => string;
    onToggle: (vehicle: VehiclePrefab, selected: boolean) => void;
}

const sameEntity = (a: VehiclePrefab, b: VehiclePrefab) =>
    a.entity.index === b.entity.index && a.entity.version === b.entity.version;

/**
 * The vehicle picker, built on the game's own Dropdown rather than a hand-rolled expander.
 *
 * Using vanilla's widget buys the popup behaviour (the menu floats over the panel instead of
 * shoving its contents down), its focus handling, and its styling. What we put INSIDE it is ours:
 * two-line rows with large thumbnails and stats, a search field, and the selected vehicles kept
 * visible below the toggle.
 */
export const VehicleDropdown = ({
    label,
    available,
    selected,
    showSecondary,
    stats,
    nameOf,
    onToggle,
}: VehicleDropdownProps) => {
    const [query, setQuery] = useState("");
    const [filters, setFilters] = useState<VehicleFilters>(NO_FILTERS);
    const [filtersOpen, setFiltersOpen] = useState(false);

    // Where to float the filter panel, in screen pixels. Measured from the toggle row when the
    // panel opens rather than laid out beside it: the info panel scrolls inside an overflow:hidden
    // container, and nothing positioned inside that can escape it -- the panel rendered at the
    // right place and was simply clipped. Vanilla's own dropdown menu portals out for the same
    // reason. Geometry reads are a frame stale in this engine, so the measurement waits one frame.
    const anchorRef = useRef<HTMLDivElement>(null);
    const [panelPos, setPanelPos] = useState<{ left: number; top: number } | null>(null);

    // Keeps the dropdown open while the player uses the filters. Vanilla's Dropdown, while open,
    // listens for mousedown on the DOCUMENT and closes if the pointer is outside the menu's
    // bounding rect -- a geometric test, so the portalled panel is "outside" no matter where it is
    // in the React tree. Stopping the native mousedown here, on the panel, means it never reaches
    // the document listener at all. A native listener rather than React's onMouseDown so it runs
    // during bubbling at this element, before the document, regardless of how React attaches.
    const panelRef = useRef<HTMLDivElement>(null);
    const stripRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const swallow = (e: MouseEvent) => e.stopPropagation();
        const els = [panelRef.current, stripRef.current].filter((el): el is HTMLDivElement => !!el);
        for (const el of els) el.addEventListener("mousedown", swallow);
        return () => {
            for (const el of els) el.removeEventListener("mousedown", swallow);
        };
    }, [panelPos]);

    const [favouritesOpen, setFavouritesOpen] = useState(false);

    // The upload whose page is open, by group id; null for none. Opening one asks C# for its
    // cache entry, which lands in the packInfo binding.
    const [packOpen, setPackOpen] = useState<string | null>(null);
    const openPack = (group: string) => {
        requestPackInfo(group);
        setPackOpen(group);
    };
    // The vehicle whose page is open. Its own column slot under the pack page, so a player
    // can go pack -> vehicle and back without losing the pack.
    const [assetOpen, setAssetOpen] = useState<VehiclePrefab | null>(null);
    const openAsset = (vehicle: VehiclePrefab) => {
        requestAssetInfo(vehicle.id);
        setAssetOpen(vehicle);
        // A one-vehicle upload is still an upload, with a PDX page of its own -- but it is
        // listed as a loose row, never under a header, so nothing else offers that page. Open
        // it alongside: the upload's page above, the vehicle's below.
        const pack = groupOf(stats(vehicle));
        if (
            options.packPages &&
            pack &&
            pack.startsWith("Mod:") &&
            available.filter((v) => groupOf(stats(v)) === pack).length === 1
        ) {
            openPack(pack);
        }
    };
    const anyPanelOpen = filtersOpen || favouritesOpen || packOpen !== null || assetOpen !== null;

    // Whether vanilla currently has the list open, reported through Dropdown's onToggle, so the
    // panels re-anchor as it opens and closes.
    const [menuOpen, setMenuOpen] = useState(false);

    useEffect(() => {
        if (!anyPanelOpen) {
            setPanelPos(null);
            return;
        }
        // Anchored to the toggle row OR the open list, whichever reaches further right: the list
        // is a shrink-to-fit popup wider than the row, and a panel anchored to the row alone sat
        // on top of it. Re-run when the list opens or closes, since that is when the edge moves.
        //
        // Measured over several frames, not one: the popup reports zero width on the frame it
        // opens and grows over the next few, so a single read right after onToggle(true) saw
        // the row alone and anchored the panels on top of the list.
        const measure = () => {
            const row = anchorRef.current?.getBoundingClientRect();
            if (!row) return;
            // Found by class, not by ref: vanilla's Dropdown re-creates the `content` element
            // when it mounts the popup, and a ref on the element we passed stays null (verified
            // live -- the menu was open at 1364px and the panel still anchored to the row).
            const menu = document.getElementsByClassName(styles.menuBody)[0]?.getBoundingClientRect();
            const right = menu && menu.width > 0 ? Math.max(row.right, menu.right) : row.right;
            setPanelPos((prev) =>
                prev && prev.left === right && prev.top === row.top ? prev : { left: right, top: row.top },
            );
        };
        // And re-measured on a timer for as long as a panel is open, because the list closing
        // is not reliably reported (onToggle did not move the panels back when the list shut,
        // verified live) and its close animation outlasts any fixed frame count. The setter
        // above only commits a changed value, so a steady state costs no renders.
        let handle = requestAnimationFrame(measure);
        const timer = setInterval(() => {
            cancelAnimationFrame(handle);
            handle = requestAnimationFrame(measure);
        }, 100);
        return () => {
            clearInterval(timer);
            cancelAnimationFrame(handle);
        };
    }, [anyPanelOpen, menuOpen]);
    const groupName = useGroupName();
    const assetText = useAssetText();

    // A folder's heading and icon: a collection is headed by its top pack's (localised) name
    // and icon; an upload by the rules in useGroupName.
    const folderTitle = (folder: string, members: VehiclePrefab[]) => {
        const s = stats(members[0]);
        return isCollection(folder)
            ? (s?.collectionTitle ? assetText.nameOf(s.collectionTitle) : folder)
            : groupName(folder, s?.groupTitle ?? "", nameOf(members[0]));
    };
    const folderIcon = (folder: string, members: VehiclePrefab[]) => {
        const s = stats(members[0]);
        return (isCollection(folder) ? s?.collectionIcon || s?.groupIcon : s?.groupIcon) || undefined;
    };
    const t = useT();
    const options = useStatOptions();
    const favourites = useFavourites();
    const favouritePacks = useFavouritePacks();
    const recentNames = useRecent();

    // The recent order as it stood when the list last opened. Refreshed on open (vanilla's
    // onToggle(true) is reliable -- the close side is not, which is why the position code polls)
    // and held for the whole time the list is open, so picks made while it is open do not move
    // the block. Seeded from the live value so a freshly mounted panel shows something.
    const [recentSnapshot, setRecentSnapshot] = useState<string[]>(recentNames);
    // Open or shut, persisted through the settings so it survives reopening the panel and the
    // game; the binding is a getter, so the toggle takes effect on the next read.
    const recentOpen = options.recentOpen;
    const setRecentOpen = (open: boolean) => setRecentOpen$(open);
    const onMenuToggle = (open: boolean) => {
        setMenuOpen(open);
        if (open) setRecentSnapshot(recentNames);
    };

    const q = query.trim().toLowerCase();

    // Matching runs against the TRANSLATED name, because that is what the player reads. Vanilla
    // renders the label through a localized asset-name lookup, so the visible text is nowhere in
    // the props -- filtering on the raw id alone would miss the name on screen. The id is matched
    // too, since modded assets often go untranslated and show their id instead.
    //
    // Then the things a player knows a vehicle BY that are not in its name: the creator, the
    // upload's title and the declared family. "bwegt" or "REV0" pulls in the whole pack even when
    // the individual names carry neither, which is the usual case for a pack of liveries.
    const haystack = (vehicle: VehiclePrefab): string => {
        const s = stats(vehicle);
        return [nameOf(vehicle), vehicle.id, s?.author, s?.groupTitle, s?.familyTitle]
            .filter((x): x is string => !!x)
            .join("\n")
            .toLowerCase();
    };
    const matches = (vehicle: VehiclePrefab) => !q || haystack(vehicle).includes(q);

    const shown = available.filter((v) => matches(v) && passesFilters(stats(v), filters));

    // Chip values come from the whole list, not the filtered one: narrowing to European stock must
    // not remove the North American chip that would undo it.
    const distinct = (pick: (v: VehiclePrefab) => string) =>
        [...new Set(available.map((v) => pick(v)).filter((x) => x !== ""))].sort();

    const themeValues = distinct((v) => stats(v)?.theme ?? "");
    // Vanilla before Modded, not alphabetical.
    const sourceValues = distinct((v) => sourceOf(stats(v))).sort((a) => (a === SOURCE_VANILLA ? -1 : 1));
    const authorValues = distinct((v) => stats(v)?.author ?? "");
    const countryValues = [...new Set(available.flatMap((v) => countriesOf(stats(v))))].sort();
    const energyValues = [
        ...new Set(
            available
                .flatMap((v) => (stats(v)?.energyType ?? "").split(","))
                .map((e) => e.trim())
                .filter((e) => e !== ""),
        ),
    ].sort();
    const isSelected = (vehicle: VehiclePrefab) => selected.some((s) => sameEntity(s, vehicle));

    // Vanilla forbids clearing the last remaining selection, or the line would be left with no
    // vehicle model at all.
    const isDisabled = (vehicle: VehiclePrefab) => isSelected(vehicle) && selected.length === 1;

    // Partition into packs, preserving the order the game emitted so grouping never reshuffles what
    // sorting (goal #2) has not been asked to reorder yet. A Map keeps first-seen group order.
    const ungrouped: VehiclePrefab[] = [];
    const groups = new Map<string, VehiclePrefab[]>();

    // Starred vehicles on this line, for the favourites panel. From the whole line, not the
    // filtered list: the panel is a shortcut past search and filters, not subject to them. They
    // stay in their packs in the list too -- the panel is a second door, and a starred row in
    // the list is how you unstar.
    const starred = available.filter((v) => favourites.has(v.id));

    // Starred packs on this line, each with every member the line offers -- so a livery added to
    // the pack later is covered without restarring. From the whole line, like the vehicles, and
    // not folded when small: the player starred the pack, so it shows as one.
    const starredPacks = new Map<string, VehiclePrefab[]>();
    const byPack = new Map<string, VehiclePrefab[]>();
    for (const v of available) {
        const pack = folderOf(stats(v));
        if (pack === null) continue;
        const all = byPack.get(pack);
        if (all) all.push(v);
        else byPack.set(pack, [v]);
        if (!favouritePacks.has(pack)) continue;
        const members = starredPacks.get(pack);
        if (members) members.push(v);
        else starredPacks.set(pack, [v]);
    }
    const anyStarred = starred.length > 0 || starredPacks.size > 0;

    // Recently picked, shown at the top of the list as a MIRROR: the vehicle stays in its pack
    // as well, so the list is still the list. An earlier version lifted recents out of their
    // packs, and a player who went to the pack for a train found it gone. Newest-first rather
    // than the player's sort, because "what did I just use" is the order that is the point.
    //
    // Built from the order captured when the list OPENED, not the live one: picking a train
    // records it, and a live block re-sorted under the cursor mid-selection ("I pick one of
    // three and the other two move"). The live order is picked up next time the list opens.
    const recent = recentSnapshot
        .map((id) => shown.find((v) => v.id === id))
        .filter((v): v is VehiclePrefab => v !== undefined);

    for (const vehicle of shown) {
        const pack = folderOf(stats(vehicle));
        if (pack === null) {
            ungrouped.push(vehicle);
            continue;
        }
        const members = groups.get(pack);
        if (members) {
            members.push(vehicle);
        } else {
            groups.set(pack, [vehicle]);
        }
    }

    // A pack with a single member on this line is not a group -- it is one vehicle wearing a
    // headline, which costs a row and a click to reach. Fold it back in with the loose entries.
    for (const [pack, members] of [...groups]) {
        if (members.length < 2) {
            ungrouped.push(...members);
            groups.delete(pack);
        }
    }

    // The player's chosen order applies to the loose rows and to the order of groups. INSIDE a
    // family the creator's index is authoritative -- "an index in the component, so you can
    // dictate the order of variants inside the group" -- so a declared family keeps its authored
    // order whatever the player picked. Only a family nobody indexed falls through to the
    // player's sort. Int32.MaxValue is "no index set".
    const UNINDEXED = 2147483647;
    const compare = comparatorFor(options.sorting, stats, nameOf);
    const byIndex = (a: VehiclePrefab, b: VehiclePrefab) =>
        (stats(a)?.index ?? UNINDEXED) - (stats(b)?.index ?? UNINDEXED);
    const order = compare ?? byIndex;

    starred.sort(order);
    ungrouped.sort(order);
    for (const members of groups.values()) {
        const authored = members.some((m) => (stats(m)?.index ?? UNINDEXED) !== UNINDEXED);
        members.sort(authored ? byIndex : order);
    }

    /**
     * Splits a package into families by the leading token of each name, giving the middle tier of
     * the pack -> family -> configuration tree.
     *
     * Returns undefined when the split would not earn its keep: one family, or every vehicle in a
     * family of its own. Either way the extra heading row would add a click without adding
     * information, so the package stays flat.
     */
    const familiesOf = (pack: string, members: VehiclePrefab[]): VehicleSubGroup[] | undefined => {
        const byFamily = new Map<string, VehiclePrefab[]>();

        // Declared families first: a creator who pointed trains at a placeholder has said what
        // the families are, so the name heuristic is not consulted. Each placeholder becomes one
        // sub-group under its name, however many members it has -- a declared family of one is
        // still the creator's heading, not noise -- and members with no placeholder stay as
        // loose rows of the upload, which VehicleGroup renders beside the families.
        //
        // Each family carries a stable star key: the placeholder GUID for a declared one, and
        // pack|title for a name-derived one -- both survive a reload, which an entity would not.
        const declared = new Map<string, VehicleSubGroup>();
        for (const vehicle of members) {
            const s = stats(vehicle);
            if (!s?.family) continue;
            const bucket = declared.get(s.family);
            if (bucket) {
                bucket.members.push(vehicle);
            } else {
                // The pack prefab's name, through the editor's localisation: a creator who filled
                // in the asset's Name field (per language) gets that shown, the raw name otherwise.
                declared.set(s.family, {
                    key: s.family,
                    title: s.familyTitle ? assetText.nameOf(s.familyTitle) : s.family,
                    members: [vehicle],
                    // The pack's own icon, else the upload's, else (in VehicleGroup) the first
                    // member's thumbnail -- so a creator who set one picture for the upload
                    // sees it on every family folder too, not a vehicle beside it.
                    icon: s.familyIcon || s.groupIcon || undefined,
                });
            }
        }
        if (declared.size > 0) {
            return [...declared.values()];
        }

        for (const vehicle of members) {
            const family = familyOf(nameOf(vehicle));
            const bucket = byFamily.get(family);
            if (bucket) {
                bucket.push(vehicle);
            } else {
                byFamily.set(family, [vehicle]);
            }
        }

        if (byFamily.size < 2 || byFamily.size === members.length) {
            return undefined;
        }

        return [...byFamily].map(([family, vehicles]) => ({
            key: `${pack}|${family}`,
            title: family,
            members: vehicles,
        }));
    };

    // Starred families: the middle tier, pinned on its own so "BR612" can be a favourite without
    // pinning the pack it ships in. Skipped inside a pack that is itself starred, where the
    // family already shows. Families exist only where the list would show them, so what can be
    // starred is exactly what has a header to star.
    const starredFamilies: VehicleSubGroup[] = [];
    for (const [pack, all] of byPack) {
        if (starredPacks.has(pack)) continue;
        for (const fam of familiesOf(pack, all) ?? []) {
            if (favouritePacks.has(fam.key)) starredFamilies.push(fam);
        }
    }
    const anyPinned = anyStarred || starredFamilies.length > 0;

    // The player's list width, applied Find It's way: a settings slider, pushed through the
    // options binding, set inline. A minimum rather than a width so wide rows still fit, and an
    // absolute value because inside the shrink-to-fit popup a percentage is circular.
    const menuStyle = options.listWidth > 0 ? { minWidth: `${options.listWidth}rem` } : undefined;
    // The scroll caps likewise; 0 leaves the stylesheet's default in place.
    const listScrollStyle = options.listHeight > 0 ? { maxHeight: `${options.listHeight}rem` } : undefined;
    const favouritesStyle = options.favouritesWidth > 0 ? { width: `${options.favouritesWidth}rem` } : undefined;
    const favouritesScrollStyle =
        options.favouritesHeight > 0 ? { maxHeight: `${options.favouritesHeight}rem` } : undefined;

    const menu = (
        <div className={styles.menuBody} style={menuStyle}>
            {available.length >= SEARCH_THRESHOLD && (
                <div className={styles.searchBar}>
                    <input
                        className={styles.search}
                        type="text"
                        value={query}
                        placeholder={t("Search", "Search...")}
                        onChange={(e) => setQuery(e.target.value)}
                    />
                </div>
            )}

            {/* The rows in the game's Scrollable, height-capped, so a long list scrolls inside a
                box of fixed size instead of running to the bottom of the screen. The search field
                stays above the scroll, always reachable. */}
            <VC.Scrollable vertical={true} className={styles.listScroll} style={listScrollStyle}>

            {/* Recent first, under a heading so the block explains itself and with a divider
                under it so the boundary with the rest is visible; then loose entries, then the
                groups -- the order the mockup shows. */}
            {recent.length > 0 && (
                // Collapsible, at Maestro's request: "it should be hidable if someone doesn't
                // like it taking space". The whole heading is the hit target, the chevron says
                // which way it goes, and the choice is remembered per player in the settings.
                <div className={styles.collapsibleHeading} onClick={() => setRecentOpen(!recentOpen)}>
                    <div className={styles.sectionHeading}>{t("RecentlyUsed", "Recently used")}</div>
                    <div
                        className={styles.headingChevron}
                        style={{ maskImage: `url(${recentOpen ? CHEVRON_UP : CHEVRON_DOWN})` }}
                    />
                </div>
            )}
            {recentOpen && recent.map((vehicle) => (
                <VehicleRow
                    key={`${vehicle.entity.index}:${vehicle.entity.version}`}
                    vehicle={vehicle}
                    name={nameOf(vehicle)}
                    selected={isSelected(vehicle)}
                    disabled={isDisabled(vehicle)}
                    showSecondary={showSecondary}
                    stats={stats}
                    onToggle={onToggle}
                    onInfo={options.vehiclePages ? () => openAsset(vehicle) : undefined}
                />
            ))}
            {recentOpen && recent.length > 0 && recent.length < shown.length && (
                <div className={styles.starredDivider} />
            )}

            {ungrouped.map((vehicle) => (
                <VehicleRow
                    key={`${vehicle.entity.index}:${vehicle.entity.version}`}
                    vehicle={vehicle}
                    name={nameOf(vehicle)}
                    selected={isSelected(vehicle)}
                    disabled={isDisabled(vehicle)}
                    showSecondary={showSecondary}
                    stats={stats}
                    onToggle={onToggle}
                    onInfo={options.vehiclePages ? () => openAsset(vehicle) : undefined}
                />
            ))}

            {/* Groups obey the same order as the rows, keyed on their first member: after the sort
                above that member is the group's own extreme -- its earliest date, or its
                alphabetically first name -- so ordering groups by it is consistent with the
                ordering inside them. */}
            {[...groups]
                .sort(([, a], [, b]) => (compare ? compare(a[0], b[0]) : 0))
                .map(([pack, members]) => (
                <VehicleGroup
                    key={pack}
                    // REVO's rule: the headline comes from the package's parent asset. Every member
                    // reports the same one, so the first is as good as any.
                    title={folderTitle(pack, members)}
                    members={members}
                    subGroups={familiesOf(pack, members)}
                    // While searching, a shut group would hide its own matches.
                    forceOpen={q.length > 0}
                    isSelected={isSelected}
                    isDisabled={isDisabled}
                    showSecondary={showSecondary}
                    stats={stats}
                    nameOf={nameOf}
                    onToggle={onToggle}
                    onRowInfo={options.vehiclePages ? openAsset : undefined}
                    starKey={pack}
                    isPack={true}
                    icon={folderIcon(pack, members)}
                    // Only PDX uploads have a page ("Mod:<id>" groups); the setting gates it.
                    onInfo={options.packPages && pack.startsWith("Mod:") ? () => openPack(pack) : undefined}
                />
            ))}

            {/* Names the reason, because an empty list from a filter the player forgot they set
                looks like a broken mod rather than a narrow search. */}
            {shown.length === 0 && (
                <div className={styles.empty}>
                    {q.length > 0
                        ? t("NoMatchQuery", `No vehicles match "{QUERY}"`, { QUERY: query })
                        : t("NoMatchFilters", "No vehicles match the current filters")}
                </div>
            )}
            </VC.Scrollable>
        </div>
    );

    return (
        <>
            {/* The dropdown and the filter strip share a row, so the strip sits beside the toggle
                rather than inside vanilla's popup. Outside the popup means it survives the menu
                closing, and it is not subject to the popup's shrink-to-fit sizing. */}
            <div className={styles.dropdownRow} ref={anchorRef}>
              <div className={styles.dropdownHost}>
                <VC.Dropdown
                    theme={{
                        ...VT.gameDropdown,
                        dropdownMenu: c(VT.gameDropdown.dropdownMenu, styles.menuTransparent),
                    }}
                    content={menu}
                    onToggle={onMenuToggle}
                >
                <VC.DropdownToggle showHint={true}>
                    <div className={styles.toggleContent}>
                        <div className={styles.label}>{label}</div>
                        {selected.length > 0 && (
                            <div className={styles.summary}>{t("Selected", "{COUNT} selected", { COUNT: selected.length })}</div>
                        )}
                    </div>
                </VC.DropdownToggle>
                </VC.Dropdown>
              </div>

              {/* The button strip swallows mousedown like the panels do: without it, pressing
                  the funnel or the star was a click outside the list, so vanilla shut the list
                  in the same instant the panel opened. */}
              <div className={styles.buttonStrip} ref={stripRef}>
              {available.length >= SEARCH_THRESHOLD && (
                <div
                    className={c(styles.filterToggle, filtersActive(filters) ? styles.filterToggleActive : "")}
                    onClick={() => setFiltersOpen(!filtersOpen)}
                >
                    <div
                        className={styles.filterArrow}
                        style={{ backgroundImage: `url(coui://uil/Standard/FunnelFilterBold.svg)` }}
                    />
                </div>
              )}

              {/* The favourites button: same strip as the filter button, lit while its panel is
                  open. Only when something is starred on this line -- an empty panel is noise. */}
              {anyPinned && (
                <div
                    className={c(styles.filterToggle, favouritesOpen ? styles.filterToggleActive : "")}
                    onClick={() => setFavouritesOpen(!favouritesOpen)}
                >
                    <div
                        className={styles.filterArrow}
                        style={{ backgroundImage: `url(coui://uil/Standard/StarFilled.svg)` }}
                    />
                </div>
              )}
              </div>

            </div>

            {/* Portalled to the document root so the info panel's overflow:hidden cannot clip it,
                and positioned from the measured toggle row so it floats just off the panel's
                right edge -- the way Find It floats its filters beside its asset panel. One
                column holds both panels, so favourites stack under sorting and filtering when
                both are open and take its place when it is shut. */}
            {anyPanelOpen && panelPos &&
              createPortal(
                <div
                  ref={panelRef}
                  className={styles.panelColumn}
                  style={{ left: `${panelPos.left}px`, top: `${panelPos.top}px` }}
                >
                  {filtersOpen && available.length >= SEARCH_THRESHOLD && (
                    <div className={styles.filterPanel}>
                      <div className={styles.filterTitle}>{t("SortingAndFiltering", "Sorting and filtering")}</div>
                      <VehicleFiltersBar
                        filters={filters}
                        onChange={setFilters}
                        themes={themeValues}
                        energies={energyValues}
                        authors={authorValues}
                        sources={sourceValues}
                        countries={countryValues}
                      />
                    </div>
                  )}

                  {/* Starred vehicles available on this line, as ordinary rows: the same
                      checkbox writes the same selection, so a favourite is one click from
                      running without opening the list at all. Stays open when the last star is
                      removed here, so unstarring several in a row does not yank the panel away. */}
                  {favouritesOpen && (
                    <div className={c(styles.filterPanel, styles.favouritesPanel)} style={favouritesStyle}>
                      <div className={styles.filterTitle}>{t("Favourites", "Favourites")}</div>
                      {!anyPinned && (
                        <div className={styles.empty}>{t("FavouritesEmpty", "Favourite a vehicle, family or pack in the list to add it here")}</div>
                      )}
                      {/* The game's own Scrollable, height-capped: a pinned pack can hold a
                          dozen rows, and the panel would otherwise run off the screen. The
                          title stays above the scroll so it never scrolls away. */}
                      <VC.Scrollable vertical={true} className={styles.favouritesScroll} style={favouritesScrollStyle}>
                      {/* One category per tier, each under its own heading so a pinned pack,
                          family and vehicle are not mistaken for one another: packs as the same
                          collapsible groups they are in the list -- pack checkbox, families,
                          star to unpin -- then families, then vehicles as loose rows. */}
                      {starredPacks.size > 0 && (
                        <div className={styles.sectionHeading}>{t("Packs", "Packs")}</div>
                      )}
                      {[...starredPacks].map(([pack, members]) => (
                        <VehicleGroup
                          key={pack}
                          title={folderTitle(pack, members)}
                          members={members}
                          subGroups={familiesOf(pack, members)}
                          forceOpen={false}
                          isSelected={isSelected}
                          isDisabled={isDisabled}
                          showSecondary={showSecondary}
                          stats={stats}
                          nameOf={nameOf}
                          onToggle={onToggle}
                          onRowInfo={options.vehiclePages ? openAsset : undefined}
                          starKey={pack}
                          isPack={true}
                          icon={folderIcon(pack, members)}
                        />
                      ))}
                      {starredFamilies.length > 0 && (
                        <div className={styles.sectionHeading}>{t("Families", "Families")}</div>
                      )}
                      {starredFamilies.map((fam) => (
                        <VehicleGroup
                          key={fam.key}
                          title={fam.title}
                          members={fam.members}
                          forceOpen={false}
                          isSelected={isSelected}
                          isDisabled={isDisabled}
                          showSecondary={showSecondary}
                          stats={stats}
                          nameOf={nameOf}
                          onToggle={onToggle}
                          onRowInfo={options.vehiclePages ? openAsset : undefined}
                          starKey={fam.key}
                          icon={fam.icon}
                        />
                      ))}
                      {starred.length > 0 && (
                        <div className={styles.sectionHeading}>{t("Vehicles", "Vehicles")}</div>
                      )}
                      {starred.map((vehicle) => (
                        <VehicleRow
                          key={`${vehicle.entity.index}:${vehicle.entity.version}`}
                          vehicle={vehicle}
                          name={nameOf(vehicle)}
                          selected={isSelected(vehicle)}
                          disabled={isDisabled(vehicle)}
                          showSecondary={showSecondary}
                          stats={stats}
                          onToggle={onToggle}
                          onInfo={options.vehiclePages ? () => openAsset(vehicle) : undefined}
                        />
                      ))}
                      </VC.Scrollable>
                    </div>
                  )}

                  {/* The pack page, for whichever upload's info button was pressed. Its rows
                      are the upload's vehicles on this line, so picking works from the page. */}
                  {packOpen !== null && (
                    <div className={c(styles.filterPanel, styles.packPanel)}>
                      <VC.Scrollable vertical={true} className={styles.packScroll}>
                        <PackPage
                          group={packOpen}
                          title={groupName(
                            packOpen,
                            stats(available.find((v) => groupOf(stats(v)) === packOpen) ?? available[0])?.groupTitle ?? "",
                            "",
                          )}
                          members={available.filter((v) => groupOf(stats(v)) === packOpen)}
                          stats={stats}
                          nameOf={nameOf}
                          isSelected={isSelected}
                          isDisabled={isDisabled}
                          showSecondary={showSecondary}
                          onToggle={onToggle}
                          onRowInfo={options.vehiclePages ? openAsset : undefined}
                          onClose={() => setPackOpen(null)}
                        />
                      </VC.Scrollable>
                    </div>
                  )}

                  {/* The vehicle page, for whichever row's info button was pressed. */}
                  {assetOpen !== null && (
                    <div className={c(styles.filterPanel, styles.packPanel)}>
                      <VC.Scrollable vertical={true} className={styles.packScroll}>
                        <VehiclePage
                          vehicle={assetOpen}
                          stats={stats}
                          nameOf={nameOf}
                          isSelected={isSelected}
                          isDisabled={isDisabled}
                          showSecondary={showSecondary}
                          onToggle={onToggle}
                          onOpenPack={(() => {
                            const pack = groupOf(stats(assetOpen));
                            return options.packPages && pack && pack.startsWith("Mod:") ? () => openPack(pack) : undefined;
                          })()}
                          onClose={() => setAssetOpen(null)}
                        />
                      </VC.Scrollable>
                    </div>
                  )}
                </div>,
                document.body,
              )}

            {/* Outside the dropdown, exactly as vanilla had it: the selection stays readable while
                the menu is shut. */}
            {selected.length > 0 && (
                <div className={styles.pills}>
                    {selected.map((vehicle) => (
                        <div
                            className={styles.pill}
                            key={`${vehicle.entity.index}:${vehicle.entity.version}`}
                        >
                            <div
                                className={styles.pillThumb}
                                style={{ backgroundImage: `url(${thumbnailOf(vehicle, stats)})` }}
                            />
                            <div className={styles.pillLabel}>{nameOf(vehicle)}</div>
                        </div>
                    ))}
                </div>
            )}
        </>
    );
};
