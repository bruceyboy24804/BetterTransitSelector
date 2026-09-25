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

const SEARCH_THRESHOLD = 6;

const ARROW_OPEN = "Media/Glyphs/ThickStrokeArrowRight.svg";
const CHEVRON_UP = "Media/Glyphs/ThickStrokeArrowUp.svg";
const CHEVRON_DOWN = "Media/Glyphs/ThickStrokeArrowDown.svg";
const ARROW_CLOSE = "Media/Glyphs/ThickStrokeArrowLeft.svg";

export interface VehicleDropdownProps {
    label: string;
    available: VehiclePrefab[];
    selected: VehiclePrefab[];

    isPrimary: boolean;

    showSecondary: boolean;
    stats: StatsLookup;
    nameOf: (vehicle: VehiclePrefab) => string;
    onToggle: (vehicle: VehiclePrefab, selected: boolean) => void;
}

const sameEntity = (a: VehiclePrefab, b: VehiclePrefab) =>
    a.entity.index === b.entity.index && a.entity.version === b.entity.version;

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

    const anchorRef = useRef<HTMLDivElement>(null);
    const [panelPos, setPanelPos] = useState<{ left: number; top: number } | null>(null);

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

    const [packOpen, setPackOpen] = useState<string | null>(null);
    const openPack = (group: string) => {
        requestPackInfo(group);
        setPackOpen(group);
    };

    const [assetOpen, setAssetOpen] = useState<VehiclePrefab | null>(null);
    const openAsset = (vehicle: VehiclePrefab) => {
        requestAssetInfo(vehicle.id);
        setAssetOpen(vehicle);

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

    const [menuOpen, setMenuOpen] = useState(false);

    useEffect(() => {
        if (!anyPanelOpen) {
            setPanelPos(null);
            return;
        }

        const measure = () => {
            const row = anchorRef.current?.getBoundingClientRect();
            if (!row) return;

            const menu = document.getElementsByClassName(styles.menuBody)[0]?.getBoundingClientRect();
            const right = menu && menu.width > 0 ? Math.max(row.right, menu.right) : row.right;
            setPanelPos((prev) =>
                prev && prev.left === right && prev.top === row.top ? prev : { left: right, top: row.top },
            );
        };

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

    const [recentSnapshot, setRecentSnapshot] = useState<string[]>(recentNames);

    const recentOpen = options.recentOpen;
    const setRecentOpen = (open: boolean) => setRecentOpen$(open);
    const onMenuToggle = (open: boolean) => {
        setMenuOpen(open);
        if (open) setRecentSnapshot(recentNames);
    };

    const q = query.trim().toLowerCase();

    const haystack = (vehicle: VehiclePrefab): string => {
        const s = stats(vehicle);
        return [nameOf(vehicle), vehicle.id, s?.author, s?.groupTitle, s?.familyTitle]
            .filter((x): x is string => !!x)
            .join("\n")
            .toLowerCase();
    };
    const matches = (vehicle: VehiclePrefab) => !q || haystack(vehicle).includes(q);

    const shown = available.filter((v) => matches(v) && passesFilters(stats(v), filters));

    const distinct = (pick: (v: VehiclePrefab) => string) =>
        [...new Set(available.map((v) => pick(v)).filter((x) => x !== ""))].sort();

    const themeValues = distinct((v) => stats(v)?.theme ?? "");

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

    const isDisabled = (vehicle: VehiclePrefab) => isSelected(vehicle) && selected.length === 1;

    const ungrouped: VehiclePrefab[] = [];
    const groups = new Map<string, VehiclePrefab[]>();

    const starred = available.filter((v) => favourites.has(v.id));

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

    for (const [pack, members] of [...groups]) {
        if (members.length < 2) {
            ungrouped.push(...members);
            groups.delete(pack);
        }
    }

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

    const familiesOf = (pack: string, members: VehiclePrefab[]): VehicleSubGroup[] | undefined => {
        const byFamily = new Map<string, VehiclePrefab[]>();

        const declared = new Map<string, VehicleSubGroup>();
        for (const vehicle of members) {
            const s = stats(vehicle);
            if (!s?.family) continue;
            const bucket = declared.get(s.family);
            if (bucket) {
                bucket.members.push(vehicle);
            } else {
                declared.set(s.family, {
                    key: s.family,
                    title: s.familyTitle ? assetText.nameOf(s.familyTitle) : s.family,
                    members: [vehicle],

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

    const starredFamilies: VehicleSubGroup[] = [];
    for (const [pack, all] of byPack) {
        if (starredPacks.has(pack)) continue;
        for (const fam of familiesOf(pack, all) ?? []) {
            if (favouritePacks.has(fam.key)) starredFamilies.push(fam);
        }
    }
    const anyPinned = anyStarred || starredFamilies.length > 0;

    const menuStyle = options.listWidth > 0 ? { minWidth: `${options.listWidth}rem` } : undefined;

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

            <VC.Scrollable vertical={true} className={styles.listScroll} style={listScrollStyle}>

            {recent.length > 0 && (

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

            {[...groups]
                .sort(([, a], [, b]) => (compare ? compare(a[0], b[0]) : 0))
                .map(([pack, members]) => (
                <VehicleGroup
                    key={pack}

                    title={folderTitle(pack, members)}
                    members={members}
                    subGroups={familiesOf(pack, members)}

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

                    onInfo={options.packPages && pack.startsWith("Mod:") ? () => openPack(pack) : undefined}
                />
            ))}

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

                  {favouritesOpen && (
                    <div className={c(styles.filterPanel, styles.favouritesPanel)} style={favouritesStyle}>
                      <div className={styles.filterTitle}>{t("Favourites", "Favourites")}</div>
                      {!anyPinned && (
                        <div className={styles.empty}>{t("FavouritesEmpty", "Favourite a vehicle, family or pack in the list to add it here")}</div>
                      )}

                      <VC.Scrollable vertical={true} className={styles.favouritesScroll} style={favouritesScrollStyle}>

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
