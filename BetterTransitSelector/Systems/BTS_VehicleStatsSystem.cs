namespace BetterTransitSelector.Systems {
    #region Using Statements

    using System.Collections.Generic;
    using System.Linq;

    using BetterTransitSelector.Domain;
    using BetterTransitSelector.Prefabs;

    using Colossal.Entities;
    using Colossal.Serialization.Entities;
    using Colossal.UI;

    using Game;
    using Game.Prefabs;
    using Game.UI;

    using ModsCommon.Extensions;
    using ModsCommon.Systems;

    using Unity.Collections;
    using Unity.Entities;

    using UnityEngine;

    #endregion

    /// <summary>
    /// Publishes per-vehicle-prefab stats (top speed, passenger capacity) to the UI, so the carriage
    /// selector can show them on each row. Vanilla's own payload carries neither.
    /// </summary>
    /// <remarks>
    /// This is a prefab table, not per-selection state: the numbers are fixed properties of the
    /// prefab, so it is built once per load rather than recomputed as the player clicks around.
    /// It is rebuilt on <see cref="OnGameLoadingComplete"/> because prefab entities are recreated
    /// across loads — an entity captured before a load is a dangling key afterwards.
    /// </remarks>
    public partial class BTS_VehicleStatsSystem : CommonUISystemBase {
        /// <inheritdoc/>
        protected override string ModId => Mod.Instance.Id;

        private EntityQuery m_VehiclePrefabQuery;

        private EntityQuery m_PackagedPrefabQuery;

        private EntityQuery m_VariantQuery;

        private EntityQuery m_PackQuery;

        private ModsCommon.Extensions.ValueBindingHelper<VehicleStats[]> m_Stats;

        private ModsCommon.Extensions.ValueBindingHelper<string[]> m_FavouritesBinding;

        private readonly Favourites m_Favourites = new Favourites("Favourites.json");

        private ModsCommon.Extensions.ValueBindingHelper<string[]> m_FavouritePacksBinding;

        /// <summary>Starred packs, by group id -- the upload's "Mod:&lt;id&gt;" prefab name.</summary>
        private readonly Favourites m_FavouritePacks = new Favourites("FavouritePacks.json");

        private ModsCommon.Extensions.ValueBindingHelper<string[]> m_RecentBinding;

        private readonly RecentVehicles m_Recent = new RecentVehicles();

        private PrefabSystem m_PrefabSystem;

        /// <summary>PDX mod id to published title. Loaded once; the file does not change in-session.</summary>
        private Dictionary<string, string> m_PackageTitles = new Dictionary<string, string>();

        /// <summary>PDX mod id to publication date, ISO 8601 UTC. Sorts correctly as a string.</summary>
        private readonly Dictionary<string, string> m_PackageDates = new Dictionary<string, string>();

        /// <summary>PDX mod id to the creator who published it. Filterable, and no blacklist.</summary>
        private readonly Dictionary<string, string> m_PackageAuthors = new Dictionary<string, string>();

        /// <summary>PDX mod id to its whole cache entry, for the pack page.</summary>
        private readonly Dictionary<string, Newtonsoft.Json.Linq.JToken> m_PackageItems = new Dictionary<string, Newtonsoft.Json.Linq.JToken>();

        private ModsCommon.Extensions.ValueBindingHelper<PackInfo> m_PackInfo;

        private ModsCommon.Extensions.ValueBindingHelper<AssetInfo> m_AssetInfo;

        private bool m_Dirty;

        /// <summary>Old-style packs already warned about this session, so the log gets one line per pack.</summary>
        private readonly HashSet<Entity> m_WarnedLegacyPacks = new HashSet<Entity>();

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_PrefabSystem  = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_PackageTitles = LoadPackageTitles();

            // Every public transport vehicle prefab. PublicTransportVehicleData is what makes a
            // prefab selectable as a transit vehicle at all, so it is both the filter and the
            // source of the passenger figure.
            //
            // PrefabData is enableable, and the loader disables it on prefabs a save references but
            // the install no longer has. Naming it therefore adds a filter we did not write: obsolete
            // prefabs drop out. That is what we want here — no point publishing stats for a vehicle
            // that cannot appear in the list — but it is worth knowing it is happening.
            m_VehiclePrefabQuery = SystemAPI.QueryBuilder()
                                            .WithAll<PublicTransportVehicleData, PrefabData>()
                                            .Build();

            // Every packaged prefab, vehicle or not. Wider than the vehicle query on purpose: the
            // headline for a group is the package's parent asset, which by definition is NOT
            // loadout-eligible and therefore never appears in the vehicle query at all.
            m_PackagedPrefabQuery = SystemAPI.QueryBuilder()
                                             .WithAll<ModPrerequisiteData, PrefabData>()
                                             .Build();

            // Pack prefabs (GroupPrefab + BTS_Pack), found so their derived member lists can be
            // rebuilt each load.
            m_PackQuery = SystemAPI.QueryBuilder()
                                        .WithAll<BTS_PackData, PrefabData>()
                                        .Build();

            // Vehicles carrying the variant component, i.e. that point at a placeholder.
            m_VariantQuery = SystemAPI.QueryBuilder()
                                           .WithAll<BTS_VariantData, PrefabData>()
                                           .Build();

            m_Stats = CreateBinding("vehicleStats", new VehicleStats[0]);

            // A getter binding, unlike the stats table: the settings can change at any moment and
            // the getter form re-reads them, so a checkbox takes effect without a reload. The stats
            // table cannot work this way -- it is far too big to rebuild per frame.
            CreateBinding("statOptions", () => new StatOptions((Setting)Mod.Instance.Settings));

            // The sort order is chosen in the filter panel but lives in the settings, so it persists.
            // The getter binding above picks the change up on its next read.
            CreateTrigger<string>("setSorting", name => {
                if (System.Enum.TryParse<Setting.SortOrder>(name, out var order)) {
                    var setting = (Setting)Mod.Instance.Settings;
                    setting.Sorting = order;
                    setting.ApplyAndSave();
                }
            });

            // Favourites: a persisted set of prefab names, pushed as a plain array and toggled by
            // name from the row's star. Loaded here rather than on game load because the file is
            // per-player, not per-save, and OnCreate runs once. The logger is safe by now: OnCreate
            // runs after the mod's OnLoad set PrefixedLogger.DefaultLog.
            m_Favourites.Load();
            m_FavouritesBinding = CreateBinding("favourites", m_Favourites.Names.ToArray());
            CreateTrigger<string>("toggleFavourite", name => {
                m_Favourites.Toggle(name);
                m_FavouritesBinding.Value = m_Favourites.Names.ToArray();
            });

            m_FavouritePacks.Load();
            m_FavouritePacksBinding = CreateBinding("favouritePacks", m_FavouritePacks.Names.ToArray());
            CreateTrigger<string>("toggleFavouritePack", id => {
                m_FavouritePacks.Toggle(id);
                m_FavouritePacksBinding.Value = m_FavouritePacks.Names.ToArray();
            });

            // The pack page: the UI asks for one upload by its group id and the page's data is
            // pushed back. Built on request rather than for every upload up front, since most are
            // never opened and a long description per item is the bulk of the cache.
            m_PackInfo = CreateBinding("packInfo", new PackInfo());
            CreateTrigger<string>("requestPackInfo", group => {
                try {
                    m_PackInfo.Value = BuildPackInfo(group);
                } catch (System.Exception e) {
                    // A malformed cache entry costs that one page, not the trigger.
                    m_Log.Warn($"requestPackInfo({group}) -- {e.Message}");
                    m_PackInfo.Value = new PackInfo { Group = group ?? string.Empty };
                }
            });

            // The vehicle page, one tier down from the pack page: asked for by prefab name, the
            // string vanilla sends as a row's id. Same on-request shape, since the carriage list
            // is the one thing here not already in the stats table.
            m_AssetInfo = CreateBinding("assetInfo", new AssetInfo());
            CreateTrigger<string>("requestAssetInfo", id => {
                try {
                    m_AssetInfo.Value = BuildAssetInfo(id);
                } catch (System.Exception e) {
                    m_Log.Warn($"requestAssetInfo({id}) -- {e.Message}");
                    m_AssetInfo.Value = new AssetInfo { Id = id ?? string.Empty };
                }
            });

            // Links on the page open in the system browser, the way the game's own mod browser
            // does it.
            CreateTrigger<string>("openUrl", url => {
                if (!string.IsNullOrEmpty(url) && (url.StartsWith("https://") || url.StartsWith("http://"))) {
                    Application.OpenURL(url);
                }
            });

            // The Recently used block's open state: toggled from its heading in the list, stored
            // in the settings so it survives the panel closing and the game restarting.
            CreateTrigger<bool>("setRecentOpen", open => {
                var setting = (Setting)Mod.Instance.Settings;
                setting.RecentOpen = open;
                setting.ApplyAndSave();
            });

            // Recently picked, same shape: the UI reports a select, the list re-emits.
            m_Recent.Load();
            m_RecentBinding = CreateBinding("recent", m_Recent.Names.ToArray());
            CreateTrigger<string>("recordUsed", name => {
                m_Recent.Record(name);
                m_RecentBinding.Value = m_Recent.Names.ToArray();
            });
        }

        /// <inheritdoc/>
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode) {
            base.OnGameLoadingComplete(purpose, mode);

            RegisterPackTemplate();
            FileEditorCategories();

            // Rebuild rather than rebuilding now: prefab entities exist by this point, but doing the
            // work in OnUpdate keeps it off the load path and out of any half-initialised state.
            m_Dirty = true;
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (m_Dirty && !m_VehiclePrefabQuery.IsEmptyIgnoreFilter) {
                RebuildStats();
                m_Dirty = false;
            }

            // Pushes the binding only when the value was actually reassigned; the helper is
            // dirty-flagged, so this is not a per-frame serialization of the whole table.
            base.OnUpdate();
        }

        /// <summary>
        /// Walks every transit vehicle prefab and records its speed and capacity.
        /// </summary>
        private void RebuildStats() {
            var entities = m_VehiclePrefabQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var stats    = new List<VehicleStats>(entities.Length);
            var titles        = BuildGroupTitles();
            var byVariant     = BuildVariantFamilies();
            var groupIcons    = BuildGroupIcons();

            LogAssetPaths(entities);

            for (var i = 0; i < entities.Length; i++) {
                var entity = entities[i];

                var passengers = GetConsistPassengers(entity);

                // The group is always the upload. A declared variant does NOT replace it -- that
                // pulled a tagged train out of the .cok its untagged siblings stayed in (verified
                // live: two BR612s from one upload landed in two one-member groups). The
                // placeholder is the family within the upload, below.
                var group = GetGroup(entity);

                // The package's own filename is the creator's name for the upload, so it beats
                // both the "Mod:157217" prefab name and the parent-asset guess. The heuristic
                // stays as the fallback for anything not published as a .cok.
                var title = GetPackageName(entity);
                if (string.IsNullOrEmpty(title)) {
                    titles.TryGetValue(GetGroupEntity(entity), out title);
                }

                // A declared variant carries its family (the placeholder) and its own index;
                // anything else has neither. Not the vanilla UI priority as a fallback: measured
                // live, every vehicle prefab carries priority 1, so it orders nothing and would
                // only make every family look creator-ordered to the UI, which then keeps the
                // player's chosen sort out of families nobody indexed.
                var family      = string.Empty;
                var familyTitle = string.Empty;
                var familyIcon  = string.Empty;
                var index       = int.MaxValue;
                if (byVariant.TryGetValue(entity, out var variant)) {
                    family      = variant.Key;
                    familyTitle = variant.Title;
                    familyIcon  = variant.Icon;
                    index       = variant.Index;
                }

                groupIcons.TryGetValue(GetGroupEntity(entity), out var groupIcon);

                GetDynamics(entity, out var acceleration, out var braking);

                stats.Add(new VehicleStats(
                    entity, GetMaxSpeedKph(entity), passengers, group, title ?? string.Empty,
                    family, familyTitle, familyIcon, groupIcon ?? string.Empty, index,
                    acceleration, braking, GetEnergyType(entity), GetConsistCars(entity),
                    GetConsistLength(entity), GetPackageDate(entity), GetTheme(entity), GetAuthor(entity),
                    GetCountries(entity), GetRowIcon(entity),
                    variant.Collection ?? string.Empty, variant.CollectionTitle ?? string.Empty, variant.CollectionIcon ?? string.Empty,
                    GetUnitCount(entity)));
            }

            entities.Dispose();

            m_Stats.Value = stats.ToArray();

            // Which package got which headline, so a wrong-looking group name can be traced to the
            // asset it was taken from rather than guessed at.
            foreach (var pair in titles) {
                m_Log.Debug($"RebuildStats() -- headline '{m_PrefabSystem.GetPrefabName(pair.Key)}' -> '{pair.Value}'");
            }
        }

        /// <summary>
        /// Resolves the asset packs a vehicle prefab belongs to, by name.
        /// </summary>
        /// <remarks>
        /// The buffer is the game's own packaging relation: an asset author attaches
        /// <c>AssetPackItem</c> listing one or more <c>AssetPackPrefab</c>s, and the game writes
        /// each one into <c>AssetPackElement</c> on the prefab entity at load. Most prefabs carry no
        /// buffer at all, which is a normal state and not a fault.
        ///
        /// Names, not entities, cross the binding: prefab entities are recreated across loads, so an
        /// entity sent to the UI is a dangling key after the next save load.
        /// </remarks>
        /// <param name="prefab">The vehicle prefab entity.</param>
        /// <returns>The pack names, or an empty array when the vehicle is in no pack.</returns>
        /// <summary>
        /// PDX mod id to the title its creator published it under, loaded once per session.
        /// </summary>
        /// <remarks>
        /// The package filename is only the creator's internal name for the file, which is why
        /// headings came out as "flirt160dbregio1" and "GenericICx". The launcher keeps the real
        /// titles in <c>.cache/Mods/pdx_mods_cache.json</c>, keyed by the same id an asset reports
        /// as its <c>platformID</c>, so "157217" resolves to "S-Bahn Berlin | BR481, 483/484".
        ///
        /// Read from disk rather than from the PDX SDK because the SDK surfaces this through its
        /// mods-browser UI, which is asynchronous and network-backed; this file is local, present
        /// before the first frame, and needs no callback.
        ///
        /// The author is carried alongside the title: it is the closest thing to the creator id the
        /// asset creators asked for, for deciding whether two uploads belong to the same hand.
        /// </remarks>
        private Dictionary<string, string> LoadPackageTitles() {
            var titles = new Dictionary<string, string>();

            try {
                var path = System.IO.Path.Combine(
                    Colossal.PSI.Environment.EnvPath.kUserDataPath, ".cache", "Mods", "pdx_mods_cache.json");

                if (!System.IO.File.Exists(path)) {
                    m_Log.Debug($"LoadPackageTitles() -- no cache at {path}; falling back to package filenames.");
                    return titles;
                }

                var root  = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(path));
                var items = root["items"] as Newtonsoft.Json.Linq.JArray;

                if (items == null) {
                    return titles;
                }

                foreach (var item in items) {
                    var id = (string)item["id"];
                    if (string.IsNullOrEmpty(id)) {
                        continue;
                    }

                    // The whole item is kept for the pack page, built on demand from it.
                    m_PackageItems[id] = item;

                    var title = (string)item["displayName"];
                    if (!string.IsNullOrEmpty(title)) {
                        titles[id] = title;
                    }

                    // Kept as the ISO 8601 string it arrives as. Those sort correctly by ordinary
                    // string comparison when they are all UTC, which these are ("...Z"), so sorting
                    // by date needs no parsing and no date type crossing the binding.
                    var created = (string)item["creationDate"];
                    if (!string.IsNullOrEmpty(created)) {
                        m_PackageDates[id] = created;
                    }

                    var author = (string)item["author"];
                    if (!string.IsNullOrEmpty(author)) {
                        m_PackageAuthors[id] = author;
                    }
                }
            } catch (System.Exception e) {
                // A missing or malformed cache costs better headings, nothing more, so it must not
                // take the stats table down with it.
                m_Log.Warn($"LoadPackageTitles() -- could not read the mods cache: {e.Message}");
            }

            return titles;
        }

        /// <summary>
        /// The vehicle page for one asset: its file's metadata and its consist, car by car;
        /// <see cref="AssetInfo.Valid"/> false when no prefab of that name is loaded.
        /// </summary>
        /// <remarks>
        /// A vanilla vehicle has a prefab but no asset file of its own worth naming (it comes out
        /// of the game's bundles), so the file block is left empty for it and the page shows the
        /// consist alone. The consist lists the head car first and then each carriage entry with
        /// its maximum count, the same figures <see cref="SumConsist"/> totals -- so a player can
        /// see where "834 passengers" comes from, and which cars a creator built as bogies.
        /// </remarks>
        /// <param name="id">The prefab name, as vanilla sends it for a row.</param>
        private AssetInfo BuildAssetInfo(string id) {
            var info = new AssetInfo { Id = id ?? string.Empty };
            // By name over the stats table rather than a PrefabID lookup: a PrefabID needs the
            // concrete type name (TrainPrefab, CarPrefab, ...) and a row's id does not carry it.
            var prefabBase = string.IsNullOrEmpty(id) ? null : FindPrefabByName(id);
            if (prefabBase == null) {
                return info;
            }

            var entity = m_PrefabSystem.GetEntity(prefabBase);
            info.Valid = true;

            if (prefabBase.asset != null) {
                var meta       = prefabBase.asset.GetMeta();
                info.FileName  = meta.fileName ?? string.Empty;
                info.Size      = meta.size;
                info.Created   = meta.creationTime == default ? string.Empty : meta.creationTime.ToUniversalTime().ToString("o");
                info.Modified  = meta.lastWriteTime == default ? string.Empty : meta.lastWriteTime.ToUniversalTime().ToString("o");
                info.Own       = meta.belongsToCurrentUser;
            }

            info.Cars.Add(DescribeCar(entity, 1));
            if (EntityManager.TryGetBuffer(entity, true, out DynamicBuffer<VehicleCarriageElement> carriages)) {
                for (var i = 0; i < carriages.Length; i++) {
                    if (carriages[i].m_Prefab != Entity.Null) {
                        info.Cars.Add(DescribeCar(carriages[i].m_Prefab, carriages[i].m_Count.y));
                    }
                }
            }

            return info;
        }

        private (string, int, int, float) DescribeCar(Entity car, int count) => (
            m_PrefabSystem.GetPrefabName(car),
            count,
            EntityManager.TryGetComponent(car, out PublicTransportVehicleData d) ? d.m_PassengerCapacity : 0,
            EntityManager.TryGetComponent(car, out ObjectGeometryData g) ? g.m_Size.z : 0f);

        /// <summary>The loaded vehicle prefab with this name, or null. A name lookup over the stats table's entities.</summary>
        private PrefabBase FindPrefabByName(string id) {
            var stats = m_Stats.Value;
            for (var i = 0; i < stats.Length; i++) {
                if (m_PrefabSystem.GetPrefabName(stats[i].Entity) == id
                    && m_PrefabSystem.TryGetPrefab<PrefabBase>(stats[i].Entity, out var prefabBase)) {
                    return prefabBase;
                }
            }

            return null;
        }

        /// <summary>
        /// The pack page for one upload, from its cache entry; <see cref="PackInfo.Valid"/> false
        /// when the group is not a PDX upload or the cache has no entry for it.
        /// </summary>
        /// <param name="group">The group id, i.e. the "Mod:&lt;pdxId&gt;" prefab name.</param>
        private PackInfo BuildPackInfo(string group) {
            var info = new PackInfo { Group = group ?? string.Empty };
            if (string.IsNullOrEmpty(group) || !group.StartsWith("Mod:")) {
                return info;
            }

            var id = group.Substring(4);
            if (!m_PackageItems.TryGetValue(id, out var item)) {
                return info;
            }

            // The cache is not consistent about shapes: "forumLinks" is a string on one entry and
            // an array on the next (verified live -- an array threw "Can not convert Array to
            // String"). A plain value is read as-is; an array yields its first string; anything
            // else is empty rather than an exception that kills the page.
            string S(string key) {
                var token = item[key];
                switch (token) {
                    case null:
                        return string.Empty;
                    case Newtonsoft.Json.Linq.JValue value:
                        return value.Type == Newtonsoft.Json.Linq.JTokenType.Null ? string.Empty : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    case Newtonsoft.Json.Linq.JArray array:
                        foreach (var element in array) {
                            if (element is Newtonsoft.Json.Linq.JValue v && v.Type == Newtonsoft.Json.Linq.JTokenType.String) {
                                return (string)v;
                            }
                        }
                        return string.Empty;
                    default:
                        return string.Empty;
                }
            }

            info.Valid            = true;
            info.Id               = id;
            info.Title            = S("displayName");
            info.Author           = S("author");
            info.Created          = S("creationDate");
            info.Updated          = S("latestUpdate");
            info.Version          = S("version");
            info.ShortDescription = S("shortDescription");
            info.LongDescription  = S("longDescription");
            info.Rating           = float.TryParse(S("rating"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rating) ? rating : 0f;
            info.RatingsTotal     = int.TryParse(S("ratingsTotal"), out var ratings) ? ratings : 0;
            info.Subscriptions    = int.TryParse(S("subscriptionsTotal"), out var subs) ? subs : 0;
            info.Size             = long.TryParse(S("size"), out var size) ? size : 0L;
            info.Cover            = S("thumbnailPath");
            info.ForumLink        = S("forumLinks");

            if (item["screenshots"] is Newtonsoft.Json.Linq.JArray shots) {
                foreach (var shot in shots) {
                    var url = (string)shot["image"];
                    if (!string.IsNullOrEmpty(url)) {
                        info.Screenshots.Add(url);
                    }
                }
            }

            if (item["externalLinks"] is Newtonsoft.Json.Linq.JArray links) {
                foreach (var link in links) {
                    var url = (string)link["url"];
                    if (!string.IsNullOrEmpty(url)) {
                        info.Links.Add(((string)link["type"] ?? string.Empty, url));
                    }
                }
            }

            if (item["dependencies"] is Newtonsoft.Json.Linq.JArray deps) {
                foreach (var dep in deps) {
                    var depId = (string)dep["id"];
                    if (!string.IsNullOrEmpty(depId)) {
                        // Installed means the cache has it, which is what subscribed means here.
                        info.Dependencies.Add((depId, (string)dep["displayName"] ?? depId, m_PackageItems.ContainsKey(depId)));
                    }
                }
            }

            return info;
        }

        /// <summary>
        /// The upload's own package name, e.g. "S-Bahn_Berlin", or empty when unavailable.
        /// </summary>
        /// <remarks>
        /// Read from the .cok file the asset was published in. Verified live: a packaged asset's
        /// meta path is
        /// <c>…/.cache/Mods/pdx_mods/157217_1/S-Bahn_Berlin.cok</c>, so the filename is the name the
        /// creator gave their upload — far better as a heading than the "Mod:157217" prefab name or
        /// than guessing at a parent asset.
        ///
        /// Two things get stripped. The extension, and a trailing "_" plus 32 hex characters, which
        /// the publishing pipeline appends to some packages
        /// (<c>BR159_0b9ab89e1aa9cb9ed17a331cfd83289b.cok</c>) and which is noise to a player.
        /// </remarks>
        private string GetPackageName(Entity prefab) {
            if (!EntityManager.TryGetComponent(prefab, out PrefabData prefabData)
                || !m_PrefabSystem.TryGetPrefab(prefabData, out PrefabBase prefabBase)
                || prefabBase.asset == null) {
                return string.Empty;
            }

            var meta = prefabBase.asset.GetMeta();

            // The published title first: it is what the creator named the upload on PDX, where the
            // filename is only what they called the file. Cached lookups, so this is cheap.
            if (!string.IsNullOrEmpty(meta.platformID)
                && m_PackageTitles.TryGetValue(meta.platformID, out var published)) {
                return published;
            }

            var path = meta.path;
            if (string.IsNullOrEmpty(path)) {
                return string.Empty;
            }

            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(name)) {
                return string.Empty;
            }

            var underscore = name.LastIndexOf('_');
            if (underscore > 0 && name.Length - underscore - 1 == 32 && IsHex(name, underscore + 1)) {
                name = name.Substring(0, underscore);
            }

            return name;
        }

        /// <summary>Whether every character from <paramref name="start"/> onward is a hex digit.</summary>
        private static bool IsHex(string value, int start) {
            for (var i = start; i < value.Length; i++) {
                var c = value[i];
                if ((c < '0' || c > '9') && (c < 'a' || c > 'f') && (c < 'A' || c > 'F')) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Logs the asset paths behind a sample of packaged vehicles.
        /// </summary>
        /// <remarks>
        /// A probe, not a feature. The creators want a nested listing — pack, then train type, then
        /// configuration — and this mod currently produces one flat level. An asset carries an
        /// <c>AssetDataPath</c> of <c>subPath</c> plus name, which renders as a folder path inside
        /// the package, so if packaging preserves the folders a creator organised their assets into,
        /// that path IS the hierarchy and nothing needs inventing.
        ///
        /// Whether it survives publishing is unknown, and it decides the whole design: a populated
        /// subPath means the tree can be read from the game, while an empty one means the nesting
        /// has to be carried some other way. Logging it costs one line and settles it on the next
        /// load; guessing would cost a redesign.
        /// </remarks>
        private void LogAssetPaths(NativeArray<Entity> entities) {
            var logged = 0;

            for (var i = 0; i < entities.Length && logged < 12; i++) {
                if (!m_PrefabSystem.TryGetPrefab(entities[i], out PrefabBase prefab) || prefab.asset == null) {
                    continue;
                }

                var meta = prefab.asset.GetMeta();
                m_Log.Debug(
                    $"LogAssetPaths() -- '{prefab.name}' subPath='{prefab.asset.subPath}' " +
                    $"path='{meta.path}' packaged={meta.packaged} platformID='{meta.platformID}'");
                logged++;
            }
        }

        /// <summary>The name the template pack is registered under; what creators look for.</summary>
        private const string PackTemplateName = "BTS Pack";

        // The editor asset browser's "Better Transit Selector" folder and its three sub-folders:
        // the template to duplicate, the packs the creator made, and packs that came from someone
        // else. A "/" in an override path is a sub-category, per EditorAssetCategorySystem.
        private const string CategoryTemplate = "Better Transit Selector/Template";
        private const string CategoryMine     = "Better Transit Selector/My Packs";
        private const string CategoryShared   = "Better Transit Selector/Shared Packs";

        private bool m_TemplateRegistered;

        /// <summary>
        /// Registers a template train pack so creators can duplicate it in the editor.
        /// </summary>
        /// <remarks>
        /// A prefab type that belongs to a mod is not otherwise creatable in the editor: the Add
        /// Component dialog lists components only, and there is no "new prefab of type X" flow. What
        /// the editor does offer is duplicating an existing prefab, so the mod provides one to
        /// duplicate — the same technique Network Tools uses for its runtime tool prefabs.
        ///
        /// The template itself is never a family: it has no asset and therefore no GUID. Only a copy
        /// the creator saves and ships becomes one, which is the intended flow.
        /// </remarks>
        private void RegisterPackTemplate() {
            if (m_TemplateRegistered) {
                return;
            }

            // A vanilla GroupPrefab root with our component on it, never a prefab type of ours:
            // a mod root type makes the asset unloadable when the mod is disabled
            // (InvalidCastException in PrefabAsset.Load), a mod component is just dropped.
            var template  = ScriptableObject.CreateInstance<GroupPrefab>();
            template.name = PackTemplateName;
            template.AddComponent<BTS_Pack>();

            // The editor's asset browser lists a prefab only where a category's query matches it
            // or an override names a path -- and this prefab matches nothing (no ObjectData, no
            // NetData), which is why the first version was registered and still invisible. The
            // override creates a "Better Transit Selector" folder and files the template there.
            var category = template.AddComponent<EditorAssetCategoryOverride>();
            category.m_IncludeCategories = new[] { CategoryTemplate };

            // The game's UI Object, pre-attached with no icon: a creator who duplicates the
            // template finds the icon field waiting rather than having to add the component.
            // No group and no priority, so the pack never appears in a build toolbar.
            template.AddComponent<UIObject>();

            if (m_PrefabSystem.AddPrefab(template)) {
                m_TemplateRegistered = true;
            } else {
                m_Log.Warn($"RegisterPackTemplate() -- AddPrefab refused '{PackTemplateName}'.");
            }
        }

        /// <summary>
        /// Files every pack into the right editor sub-folder by where it came from: a subscribed
        /// or received pack under Shared Packs, one the creator saved themselves under My Packs.
        /// </summary>
        /// <remarks>
        /// A pack duplicated from the template inherits the template's category override, so
        /// without this every pack would sit beside the template. The override is rewritten on
        /// every load rather than at save time, because the same file means different things on
        /// different machines: the sender's own pack is the receiver's shared one. "Shared" is
        /// anything not made here -- a package from a subscription, or a file in the
        /// SharedPrefabs folder -- which is also what a creator wants to see grouped when
        /// choosing a Placeholder or Parent from someone else's collection.
        ///
        /// The category system rebuilds lazily on a private dirty flag that only prefab
        /// creation or deletion sets, so it is set here by reflection; without it the change
        /// would show only after the next prefab edit.
        /// </remarks>
        private void FileEditorCategories() {
            var packs  = m_PackQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var shared = SharedPrefabs.Folder.Replace('\\', '/');
            var seen   = new Dictionary<string, string>();

            for (var i = 0; i < packs.Length; i++) {
                if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(packs[i], out var prefabBase)) {
                    continue;
                }

                // The same asset present as two files (the game loads both, with one GUID) would
                // list twice. Only the first copy is filed; the second is named in the log so the
                // creator can delete it -- the fix is on disk, not here.
                var guid = GetAssetGuid(packs[i]);
                if (!string.IsNullOrEmpty(guid)) {
                    var here = prefabBase.asset?.path ?? string.Empty;
                    if (seen.TryGetValue(guid, out var first)) {
                        m_Log.Warn($"FileEditorCategories() -- '{prefabBase.name}' exists twice with one identity: '{first}' and '{here}'. Delete one; only the first is listed.");
                        if (prefabBase.TryGet<EditorAssetCategoryOverride>(out var dup)) {
                            dup.m_IncludeCategories = new string[0];
                        }
                        continue;
                    }
                    seen[guid] = here;
                }

                string category;
                if (prefabBase.asset == null) {
                    category = CategoryTemplate;
                } else {
                    var meta = prefabBase.asset.GetMeta();
                    var path = (meta.path ?? string.Empty).Replace('\\', '/');
                    category = meta.packaged || path.StartsWith(shared, System.StringComparison.OrdinalIgnoreCase)
                        ? CategoryShared
                        : CategoryMine;
                }

                if (!prefabBase.TryGet<EditorAssetCategoryOverride>(out var over)) {
                    over = prefabBase.AddComponent<EditorAssetCategoryOverride>();
                    EntityManager.AddComponent<EditorAssetCategoryOverrideData>(packs[i]);
                }
                over.m_IncludeCategories = new[] { category };
                // Keeping packs out of Custom Assets / Subscribed is done in
                // Patches/EditorAssetCategoryPatches: the override's exclude list is ignored for
                // those two folders (they are getter-fed, and exclusions apply to query results only).
            }

            packs.Dispose();

            World.GetExistingSystemManaged<Game.UI.Editor.EditorAssetCategorySystem>()
                 ?.SetMemberValue("m_Dirty", true);
        }

        /// <summary>
        /// Copies every pack the player made themselves into <see cref="SharedPrefabs.Folder"/>,
        /// for handing to another creator. Subscribed packs are skipped: those are someone else's
        /// to share, and they already ship inside a package.
        /// </summary>
        /// <returns>How many packs were exported.</returns>
        public int ExportSharedPacks() {
            var packs    = m_PackQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var exported = 0;

            for (var i = 0; i < packs.Length; i++) {
                if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(packs[i], out var prefabBase) || prefabBase.asset == null) {
                    continue;
                }

                var meta = prefabBase.asset.GetMeta();
                if (meta.packaged) {
                    continue;
                }

                if (SharedPrefabs.Export(prefabBase.asset.path)) {
                    exported++;
                } else {
                    m_Log.Warn($"ExportSharedPacks() -- '{prefabBase.name}' at '{prefabBase.asset.path}' has no .cid beside it; skipped.");
                }
            }

            packs.Dispose();
            return exported;
        }

        /// <summary>A resolved family: its key, its heading, its icon, and a member's order within it.</summary>
        private readonly struct Variant {
            public readonly string Key;
            public readonly string Title;
            public readonly string Icon;
            public readonly int    Index;
            public readonly string Collection;
            public readonly string CollectionTitle;
            public readonly string CollectionIcon;

            public Variant(string key, string title, string icon, int index,
                           string collection, string collectionTitle, string collectionIcon) {
                Key             = key;
                Title           = title;
                Icon            = icon;
                Index           = index;
                Collection      = collection;
                CollectionTitle = collectionTitle;
                CollectionIcon  = collectionIcon;
            }
        }

        /// <summary>
        /// The top-most pack above a placeholder, following <see cref="BTS_Pack.m_Parent"/>;
        /// the placeholder itself when it has no parent. Stops on a cycle or a parent with no
        /// entity, and logs either, so a mis-set chain degrades to "no collection" rather than
        /// hanging the load.
        /// </summary>
        private Entity GetRootPack(Entity placeholder) {
            var current = placeholder;
            var seen    = new HashSet<Entity>();

            while (seen.Add(current)
                   && m_PrefabSystem.TryGetPrefab<PrefabBase>(current, out var prefabBase)
                   && prefabBase.TryGet<BTS_Pack>(out var pack)
                   && pack.m_Parent != null) {
                if (!m_PrefabSystem.TryGetEntity(pack.m_Parent, out var parent)) {
                    m_Log.Warn($"GetRootPack() -- '{prefabBase.name}' names a parent pack with no entity; treated as the root.");
                    break;
                }
                if (seen.Contains(parent)) {
                    m_Log.Warn($"GetRootPack() -- parent chain from '{m_PrefabSystem.GetPrefabName(placeholder)}' loops at '{pack.m_Parent.name}'; stopped.");
                    break;
                }
                current = parent;
            }

            return current;
        }

        /// <summary>
        /// The picker-only icon a creator set on the vehicle's variant component, as a UI URL;
        /// empty when none, and the UI shows the vehicle's thumbnail.
        /// </summary>
        private string GetRowIcon(Entity prefab) =>
            m_PrefabSystem.TryGetPrefab<PrefabBase>(prefab, out var prefabBase)
            && prefabBase.TryGet<BTS_Variant>(out var variant)
            && !string.IsNullOrEmpty(variant.m_Icon)
                ? UIExtensions.ResolveUri(variant.m_Icon)
                : string.Empty;

        /// <summary>
        /// The icon a creator put on a prefab through the game's <c>UIObject</c> component, as a
        /// UI URL; empty when none. The same lookup vanilla's thumbnails start with.
        /// </summary>
        private string GetIcon(Entity prefab) =>
            m_PrefabSystem.TryGetPrefab<PrefabBase>(prefab, out var prefabBase)
                ? ImageSystem.GetIcon(prefabBase) ?? string.Empty
                : string.Empty;

        /// <summary>
        /// The upload icon set on a pack, raw (unresolved); empty when none.
        /// </summary>
        private string GetUploadIcon(Entity pack) {
            if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(pack, out var prefabBase)) {
                return string.Empty;
            }

            if (prefabBase.TryGet<BTS_Pack>(out var component) && !string.IsNullOrEmpty(component.m_UploadIcon)) {
                return component.m_UploadIcon;
            }

            return string.Empty;
        }

        /// <summary>
        /// Per upload, the icon for its group header: the upload icon a creator set on one of its
        /// packs, else the icon of its one iconed pack when there is exactly one.
        /// </summary>
        /// <remarks>
        /// An upload has no asset of its own to carry an icon -- the "Mod:&lt;id&gt;" prefab is
        /// the game's, not the creator's -- so a pack prefab inside it is where one can live.
        /// <see cref="BTS_Pack.m_UploadIcon"/> is a picture of its own, so the folder
        /// can differ from every family in it. Without one, only an unambiguous single candidate
        /// is borrowed, since a wrong guess is worse than the vehicle-thumbnail fallback.
        /// </remarks>
        private Dictionary<Entity, string> BuildGroupIcons() {
            var packs  = m_PackQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var chosen = new Dictionary<Entity, string>();
            var single = new Dictionary<Entity, string>();
            var counts = new Dictionary<Entity, int>();

            for (var i = 0; i < packs.Length; i++) {
                var upload = GetGroupEntity(packs[i]);
                if (upload == Entity.Null) {
                    continue;
                }

                // The dedicated upload icon, resolved the way UIObject's is so a modded path works.
                if (!chosen.ContainsKey(upload)) {
                    var uploadIcon = GetUploadIcon(packs[i]);
                    if (uploadIcon.Length > 0) {
                        chosen[upload] = UIExtensions.ResolveUri(uploadIcon);
                    }
                }

                var icon = GetIcon(packs[i]);
                if (icon.Length == 0) {
                    continue;
                }

                counts[upload] = counts.TryGetValue(upload, out var n) ? n + 1 : 1;
                single[upload] = icon;
            }

            packs.Dispose();

            foreach (var pair in counts) {
                if (pair.Value == 1 && !chosen.ContainsKey(pair.Key)) {
                    chosen[pair.Key] = single[pair.Key];
                }
            }

            return chosen;
        }

        /// <summary>
        /// Resolves every vehicle that points at a placeholder to the family that placeholder is.
        /// </summary>
        /// <remarks>
        /// The family key is the placeholder's asset GUID, and that choice is the whole design. A
        /// creator who includes the same placeholder, unchanged, in a second upload ships the same
        /// asset with the same GUID, so both uploads resolve to one family with nobody typing an id
        /// or matching a name. A placeholder that was recreated or edited is a different asset with
        /// a different GUID and forms a different family — which is exactly "as long as it remains
        /// unchanged between uploads".
        ///
        /// It is also what makes families locked by default with no lock to configure: a GUID is
        /// not guessable, so nothing can point at a placeholder its author did not hand over. A
        /// collaboration is two people sharing the file.
        ///
        /// The creator id the creators mentioned is logged beside each family for diagnostics rather
        /// than enforced: the GUID already carries the guarantee, and enforcing authorship on top
        /// would break the collaboration case it was meant to enable.
        /// </remarks>
        private Dictionary<Entity, Variant> BuildVariantFamilies() {
            RegisterVariantsWithPacks();

            var vehicles = m_VariantQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var result   = new Dictionary<Entity, Variant>();

            for (var i = 0; i < vehicles.Length; i++) {
                var vehicle = vehicles[i];

                if (!EntityManager.TryGetComponent(vehicle, out BTS_VariantData data)
                    || data.m_Placeholder == Entity.Null) {
                    continue;
                }

                var key = GetAssetGuid(data.m_Placeholder);
                if (string.IsNullOrEmpty(key)) {
                    m_Log.Warn(
                        $"BuildVariantFamilies() -- '{m_PrefabSystem.GetPrefabName(vehicle)}' points at a placeholder " +
                        "with no asset identity and is left ungrouped.");
                    continue;
                }

                if (!IsAllowedToJoin(vehicle, data.m_Placeholder)) {
                    m_Log.Warn(
                        $"BuildVariantFamilies() -- '{m_PrefabSystem.GetPrefabName(vehicle)}' (by '{GetAuthor(vehicle)}') " +
                        $"points at '{m_PrefabSystem.GetPrefabName(data.m_Placeholder)}' (by '{GetAuthor(data.m_Placeholder)}') " +
                        "but is not its author and did not ship it; refused.");
                    continue;
                }

                // A pre-0.7 pack: still a family (identity is the GUID), but it fails to load
                // for every player who has this mod disabled, and the failing game logs no asset
                // name -- so name the package here, once per pack, for the creator to fix.
#pragma warning disable CS0618
                if (m_PrefabSystem.TryGetPrefab<PrefabBase>(data.m_Placeholder, out var placeholderPrefab)
                    && placeholderPrefab is BTS_TrainPackPrefab
                    && m_WarnedLegacyPacks.Add(data.m_Placeholder)) {
#pragma warning restore CS0618
                    var pkg = placeholderPrefab.asset != null ? placeholderPrefab.asset.GetMeta().packageName : "(local)";
                    m_Log.Warn($"BuildVariantFamilies() -- '{placeholderPrefab.name}' in {pkg} is an old-style pack (BTS_TrainPackPrefab). It fails to load for players with the mod disabled; recreate it from the BTS Pack template and republish.");
                }

                // The heading is the placeholder's own name -- the one thing the creator named.
                var title = m_PrefabSystem.GetPrefabName(data.m_Placeholder) ?? string.Empty;

                // The folder above the family, when the creator asked for one. Three sources,
                // in precedence: a Parent chain whose top is Exclusive (keyed on that asset's
                // GUID -- only packs naming this very asset are in it), a Parent chain whose top
                // is not (keyed on its display name, so it merges with anything of that name), or
                // a Display name typed on the pack itself (name key, no file involved). Title and icon
                // are provisional here; CanonicaliseHeadings settles them per folder afterwards.
                var root            = GetRootPack(data.m_Placeholder);
                var collection      = string.Empty;
                var collectionTitle = string.Empty;
                var collectionIcon  = string.Empty;
                if (root != data.m_Placeholder) {
                    var rootPack  = GetPack(root);
                    var rootName  = !string.IsNullOrEmpty(rootPack?.m_DisplayName) ? rootPack.m_DisplayName : m_PrefabSystem.GetPrefabName(root) ?? string.Empty;
                    collection    = rootPack != null && rootPack.m_Exclusive
                        ? CollectionPrefix + GetAssetGuid(root)
                        : HeadingPrefix + NormaliseHeading(rootName);
                    collectionTitle = rootName;
                    var uploadIcon  = GetUploadIcon(root);
                    collectionIcon  = uploadIcon.Length > 0 ? UIExtensions.ResolveUri(uploadIcon) : GetIcon(root);
                } else {
                    var pack = GetPack(data.m_Placeholder);
                    if (!string.IsNullOrEmpty(pack?.m_DisplayName)) {
                        collection      = HeadingPrefix + NormaliseHeading(pack.m_DisplayName);
                        collectionTitle = pack.m_DisplayName.Trim();
                        var uploadIcon  = GetUploadIcon(data.m_Placeholder);
                        collectionIcon  = uploadIcon.Length > 0 ? UIExtensions.ResolveUri(uploadIcon) : string.Empty;
                    }
                }

                result[vehicle] = new Variant(key, title, GetIcon(data.m_Placeholder), data.m_Index,
                                              collection, collectionTitle, collectionIcon);
            }

            vehicles.Dispose();
            CanonicaliseHeadings(result);
            return result;
        }

        /// <summary>Folder key prefix for a GUID-keyed (exclusive) collection.</summary>
        private const string CollectionPrefix = "Col:";

        /// <summary>Folder key prefix for a name-keyed heading.</summary>
        private const string HeadingPrefix = "Heading:";

        /// <summary>The pack component on a placeholder prefab, or null.</summary>
        private BTS_Pack GetPack(Entity placeholder) =>
            m_PrefabSystem.TryGetPrefab<PrefabBase>(placeholder, out var prefabBase) && prefabBase.TryGet<BTS_Pack>(out var pack)
                ? pack
                : null;

        /// <summary>
        /// The merge key for a brand name: lower-cased, whitespace collapsed, so "DB Regio BaWü"
        /// and "db regio  bawü" are one folder. Accents are kept -- "BaWu" and "BaWü" are two
        /// spellings, and a creator can see and fix that; folding them would be a guess.
        /// </summary>
        private static string NormaliseHeading(string name) =>
            System.Text.RegularExpressions.Regex.Replace((name ?? string.Empty).Trim(), @"\s+", " ").ToLowerInvariant();

        /// <summary>
        /// Gives every name-keyed heading one heading and one icon: those of the member whose
        /// upload was published first. Members typed the name with their own casing and each
        /// may carry an icon; without a rule the folder would take whichever member happened to
        /// sort first and change as uploads came and went. Oldest-wins is stable and favours
        /// whoever established the heading.
        /// </summary>
        private void CanonicaliseHeadings(Dictionary<Entity, Variant> variants) {
            // Per folder: the oldest member's title, and the oldest ICONED member's icon --
            // separately, so a heading whose founder set no icon still gets the next one's.
            var oldest = new Dictionary<string, (string date, string title)>();
            var icons  = new Dictionary<string, (string date, string icon)>();

            foreach (var pair in variants) {
                var v = pair.Value;
                if (string.IsNullOrEmpty(v.Collection) || !v.Collection.StartsWith(HeadingPrefix)) {
                    continue;
                }

                // An unpublished (local) member has no date and sorts last, so a work in
                // progress never renames a published heading.
                var date = GetPackageDate(pair.Key);
                if (string.IsNullOrEmpty(date)) {
                    date = "9999";
                }

                if (!oldest.TryGetValue(v.Collection, out var t) || string.CompareOrdinal(date, t.date) < 0) {
                    oldest[v.Collection] = (date, v.CollectionTitle);
                }
                if (v.CollectionIcon.Length > 0
                    && (!icons.TryGetValue(v.Collection, out var ic) || string.CompareOrdinal(date, ic.date) < 0)) {
                    icons[v.Collection] = (date, v.CollectionIcon);
                }
            }

            foreach (var key in new List<Entity>(variants.Keys)) {
                var v = variants[key];
                if (!string.IsNullOrEmpty(v.Collection) && oldest.TryGetValue(v.Collection, out var canon)) {
                    icons.TryGetValue(v.Collection, out var icon);
                    variants[key] = new Variant(v.Key, v.Title, v.Icon, v.Index, v.Collection, canon.title, icon.icon ?? string.Empty);
                }
            }
        }

        /// <summary>
        /// Fills every pack's member list from the variants that name it.
        /// </summary>
        /// <remarks>
        /// The direct counterpart of what <c>ObjectInitializeSystem</c> does for prop variations: it
        /// walks every prefab carrying <c>SpawnableObjectData</c>, reads the placeholders that prefab
        /// names, and appends the prefab to each placeholder's <c>PlaceholderObjectElement</c> list.
        /// Here the variant is the vehicle, the placeholder is the pack, and the list is
        /// <see cref="BTS_PackElement"/>. The pack's membership is therefore derived, never
        /// authored -- the creators' "no list" requirement, satisfied the way the game satisfies it.
        ///
        /// Lists are cleared first because this runs on every load and prefab entities are recreated
        /// across loads; appending to a stale list would duplicate every member.
        ///
        /// The variant's reference is typed to the pack prefab, so every placeholder it can name
        /// carries the list. A stale asset from the dropped prop route (a prop with the old pack
        /// component) still groups -- identity is the asset -- but has nothing to register in, so
        /// nothing is appended and nothing is logged.
        /// </remarks>
        private void RegisterVariantsWithPacks() {
            var packs = m_PackQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < packs.Length; i++) {
                if (EntityManager.HasBuffer<BTS_PackElement>(packs[i])) {
                    EntityManager.GetBuffer<BTS_PackElement>(packs[i]).Clear();
                }
            }
            packs.Dispose();

            var vehicles = m_VariantQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            for (var i = 0; i < vehicles.Length; i++) {
                var vehicle = vehicles[i];

                if (!EntityManager.TryGetComponent(vehicle, out BTS_VariantData data)
                    || data.m_Placeholder == Entity.Null
                    || !EntityManager.HasBuffer<BTS_PackElement>(data.m_Placeholder)) {
                    continue;
                }

                EntityManager.GetBuffer<BTS_PackElement>(data.m_Placeholder)
                             .Add(new BTS_PackElement(vehicle));
            }

            vehicles.Dispose();
        }

        /// <summary>
        /// Whether a vehicle may join the family a placeholder identifies.
        /// </summary>
        /// <remarks>
        /// "Variants are locked by creator by default." The asset's identity alone does not lock
        /// anything: once a pack is published, every subscriber has the placeholder, and any of them
        /// could point a train at it. So the check the creators suggested — the creator id — is what
        /// actually closes the family. A vehicle joins when:
        ///
        /// <list type="bullet">
        /// <item>it was published by the same creator as the placeholder, whatever the upload — the
        /// author's own later liveries; or</item>
        /// <item>it was published in the same upload as the placeholder — trivially the author's,
        /// but stated so the rule does not depend on the author lookup succeeding.</item>
        /// </list>
        ///
        /// Collaboration is "they share the placeholder asset between themselves": the collaborator
        /// includes the same placeholder in their own upload, so their vehicle resolves to a
        /// placeholder published under their name and passes. Whether that holds when two uploads
        /// ship one GUID depends on how the game resolves the duplicate — if it keeps a single copy,
        /// the collaborator's vehicle would resolve to the original author's and be refused. That
        /// is an open question for a real two-upload test, and the refusal is logged loudly so it
        /// cannot fail silently.
        ///
        /// Anything without an author on either side falls open rather than shut: a local, unpublished
        /// asset has no creator id, and locking a creator out of their own work in progress would be
        /// worse than the abuse this guards against.
        /// </remarks>
        private bool IsAllowedToJoin(Entity vehicle, Entity placeholder) {
            if (GetGroupEntity(vehicle) != Entity.Null && GetGroupEntity(vehicle) == GetGroupEntity(placeholder)) {
                return true;
            }

            var vehicleAuthor     = GetAuthor(vehicle);
            var placeholderAuthor = GetAuthor(placeholder);

            if (string.IsNullOrEmpty(vehicleAuthor) || string.IsNullOrEmpty(placeholderAuthor)) {
                return true;
            }

            return vehicleAuthor == placeholderAuthor;
        }

        /// <summary>
        /// The GUID of the asset a prefab was loaded from; empty when it has none.
        /// </summary>
        private string GetAssetGuid(Entity prefab) {
            if (!EntityManager.TryGetComponent(prefab, out PrefabData prefabData)
                || !m_PrefabSystem.TryGetPrefab(prefabData, out PrefabBase prefabBase)
                || prefabBase.asset == null) {
                return string.Empty;
            }

            return prefabBase.asset.id.guid.ToString();
        }

        /// <summary>
        /// Finds each package's parent asset, keyed by its content prerequisite entity.
        /// </summary>
        /// <remarks>
        /// Implements REVO's rule: the assets carrying the UI component that puts them in the
        /// loadout list are the <em>children</em>, so the parent is the package-mate that has none.
        /// Here that test is <c>PublicTransportVehicleData</c> — the component the vanilla selector
        /// filters on — so anything the selector would list is excluded from being a headline.
        ///
        /// <c>UIObjectData</c> is required of a candidate as well, to keep the pick to real authored
        /// assets: a package also contains internal data prefabs that carry no UI presence at all,
        /// and one of those as a headline would be worse than none.
        ///
        /// Where several candidates qualify the first by name wins, purely so the choice is stable
        /// across loads rather than following chunk order.
        /// </remarks>
        private Dictionary<Entity, string> BuildGroupTitles() {
            var entities   = m_PackagedPrefabQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var titles     = new Dictionary<Entity, string>();
            var packHeaded = new HashSet<Entity>();

            for (var i = 0; i < entities.Length; i++) {
                var entity = entities[i];

                // Loadout-eligible, so this is a variant rather than the headline.
                if (EntityManager.HasComponent<PublicTransportVehicleData>(entity)) {
                    continue;
                }

                if (!EntityManager.HasComponent<UIObjectData>(entity)) {
                    continue;
                }

                if (!EntityManager.TryGetComponent(entity, out ModPrerequisiteData mod)
                    || mod.m_ContentPrerequisite == Entity.Null) {
                    continue;
                }

                var name = m_PrefabSystem.GetPrefabName(entity);
                if (string.IsNullOrEmpty(name)) {
                    continue;
                }

                // A train pack in the upload is the parent by declaration, so it beats any other
                // candidate outright; among ordinary candidates the lowest name wins for stability.
                var isPack = EntityManager.HasComponent<BTS_PackData>(entity);

                if (isPack
                    || !titles.TryGetValue(mod.m_ContentPrerequisite, out var existing)
                    || (!packHeaded.Contains(mod.m_ContentPrerequisite) && string.CompareOrdinal(name, existing) < 0)) {
                    titles[mod.m_ContentPrerequisite] = name;
                    if (isPack) {
                        packHeaded.Add(mod.m_ContentPrerequisite);
                    }
                }
            }

            entities.Dispose();
            return titles;
        }

        /// <summary>
        /// Resolves the content prerequisite entity a vehicle belongs to, or <c>Entity.Null</c>.
        /// </summary>
        private Entity GetGroupEntity(Entity prefab) {
            return EntityManager.TryGetComponent(prefab, out ModPrerequisiteData mod)
                ? mod.m_ContentPrerequisite
                : Entity.Null;
        }

        private string GetGroup(Entity prefab) {
            // The package the asset was published in. Everything from one upload shares this
            // content prerequisite prefab, which is named "Mod:<pdxId>".
            if (EntityManager.TryGetComponent(prefab, out ModPrerequisiteData mod)
                && mod.m_ContentPrerequisite != Entity.Null) {
                return m_PrefabSystem.GetPrefabName(mod.m_ContentPrerequisite) ?? string.Empty;
            }

            // Fallback for DLC content, which is bundled as an asset pack rather than a mod
            // package. Rare — one vehicle in a heavily modded load order — but free to support.
            if (EntityManager.TryGetBuffer(prefab, true, out DynamicBuffer<AssetPackElement> packs)
                && packs.Length > 0) {
                return m_PrefabSystem.GetPrefabName(packs[0].m_Pack) ?? string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// The theme this vehicle belongs to, e.g. "EU" or "NA"; empty when it is theme-agnostic.
        /// </summary>
        /// <remarks>
        /// Theme is the one filter the game itself deliberately discards here. Vanilla's list call
        /// passes <c>ignoreTheme: true</c>, and <c>VehicleSelectRequirementData.CheckRequirements</c>
        /// shows what that means: a theme requirement normally has to match the city's own theme, but
        /// with the flag set any theme passes. So the selector shows every region's stock at once,
        /// which is exactly why filtering it back down is worth having.
        ///
        /// A vehicle's theme is whichever of its <c>ObjectRequirementElement</c> entries points at a
        /// prefab carrying <c>ThemeData</c>. Those same requirement entities are what the row's flag
        /// icons already come from, so the filter and the icon agree by construction.
        /// </remarks>
        private string GetTheme(Entity prefab) {
            if (!EntityManager.TryGetBuffer(prefab, true, out DynamicBuffer<ObjectRequirementElement> requirements)) {
                return string.Empty;
            }

            for (var i = 0; i < requirements.Length; i++) {
                var requirement = requirements[i].m_Requirement;
                if (requirement != Entity.Null && EntityManager.HasComponent<ThemeData>(requirement)) {
                    return m_PrefabSystem.GetPrefabName(requirement) ?? string.Empty;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// The creator who published the vehicle's upload; empty when unknown.
        /// </summary>
        private string GetAuthor(Entity prefab) {
            if (!EntityManager.TryGetComponent(prefab, out PrefabData prefabData)
                || !m_PrefabSystem.TryGetPrefab(prefabData, out PrefabBase prefabBase)
                || prefabBase.asset == null) {
                return string.Empty;
            }

            var platformID = prefabBase.asset.GetMeta().platformID;

            return !string.IsNullOrEmpty(platformID) && m_PackageAuthors.TryGetValue(platformID, out var author)
                ? author
                : string.Empty;
        }

        /// <summary>
        /// When the vehicle's upload was first published, ISO 8601 UTC; empty when unknown.
        /// </summary>
        /// <remarks>
        /// Sorting by date is really sorting by upload, since every asset in a package shares one
        /// publication date. That is the useful granularity anyway — "what did I install recently"
        /// is a question about packs, not about individual carriages.
        /// </remarks>
        private string GetPackageDate(Entity prefab) {
            if (!EntityManager.TryGetComponent(prefab, out PrefabData prefabData)
                || !m_PrefabSystem.TryGetPrefab(prefabData, out PrefabBase prefabBase)
                || prefabBase.asset == null) {
                return string.Empty;
            }

            var platformID = prefabBase.asset.GetMeta().platformID;

            return !string.IsNullOrEmpty(platformID) && m_PackageDates.TryGetValue(platformID, out var date)
                ? date
                : string.Empty;
        }

        /// <summary>
        /// Acceleration and braking in m/s², from whichever component carries them.
        /// </summary>
        /// <remarks>
        /// Stored in m/s² already, unlike speed, so no conversion — only the same per-mode dispatch,
        /// because there is no shared base component holding them.
        /// </remarks>
        private void GetDynamics(Entity prefab, out float acceleration, out float braking) {
            if (EntityManager.TryGetComponent(prefab, out CarData car)) {
                acceleration = car.m_Acceleration;
                braking      = car.m_Braking;
            } else if (EntityManager.TryGetComponent(prefab, out TrainData train)) {
                acceleration = train.m_Acceleration;
                braking      = train.m_Braking;
            } else if (EntityManager.TryGetComponent(prefab, out WatercraftData boat)) {
                acceleration = boat.m_Acceleration;
                braking      = boat.m_Braking;
            } else {
                acceleration = 0f;
                braking      = 0f;
            }
        }

        /// <summary>
        /// What the vehicle runs on, e.g. "Electricity", or empty when no component says.
        /// </summary>
        /// <remarks>
        /// <c>EnergyTypes</c> is a flags enum, so a dual-mode vehicle reports several — ToString
        /// renders those comma-separated, which is what a player wants to read anyway.
        /// </remarks>
        private string GetEnergyType(Entity prefab) {
            if (EntityManager.TryGetComponent(prefab, out CarData car)) {
                return car.m_EnergyType.ToString();
            }

            if (EntityManager.TryGetComponent(prefab, out TrainData train)) {
                return train.m_EnergyType.ToString();
            }

            return string.Empty;
        }

        /// <summary>
        /// The number of units a multi-unit train runs as: two 3-car Mireos coupled are 2 units.
        /// 1 for anything that is not a multi-unit train.
        /// </summary>
        /// <remarks>
        /// <c>TrainEngineData.m_Count.x</c>, which is what
        /// <c>TransportVehicleSelectData.GetRandomVehicle</c> reads into its own <c>unitCount</c>
        /// and then spawns that many of. Creators name these "2x3", and without this the mod
        /// reported one unit's figures for the whole train (Maestro: "on both the carriages will
        /// be 3, which is correct but also misleading for the 2x3").
        /// </remarks>
        private int GetUnitCount(Entity prefab) =>
            EntityManager.HasComponent<MultipleUnitTrainData>(prefab)
            && EntityManager.TryGetComponent(prefab, out TrainEngineData engine)
            && engine.m_Count.x > 0
                ? engine.m_Count.x
                : 1;

        /// <summary>
        /// Sums a per-car figure over the whole consist: every unit, each unit's own car plus
        /// every carriage times how many of it that unit runs.
        /// </summary>
        /// <remarks>
        /// The reason this exists is that a multi-unit train's prefab is only its head car. Its
        /// <c>PublicTransportVehicleData</c> and geometry describe that one car, and the rest of the
        /// train is a <c>VehicleCarriageElement</c> list of carriage prefabs with counts. Verified on
        /// a 12-car ICE 4: head car 71 passengers, consist 834 — and 834 is the number the creator
        /// wrote into the asset's own name. Reporting the head car alone would have shown 71 for a
        /// train that seats 834, on exactly the kind of asset this mod exists for.
        ///
        /// Two details are taken from the game's own spawn loop rather than guessed, because both
        /// were wrong here before: the carriage count is <c>m_Count.x</c>, the number the loop
        /// actually creates (the <c>.y</c> this used is never read at spawn), and the whole thing
        /// is multiplied by <see cref="GetUnitCount"/> — a 2x2 BR612 seats twice what one unit does,
        /// which is why its row read 145 beside a name saying 290.
        /// </remarks>
        private float SumConsist(Entity prefab, System.Func<Entity, float> perCar) {
            var perUnit = perCar(prefab);

            if (EntityManager.TryGetBuffer(prefab, true, out DynamicBuffer<VehicleCarriageElement> carriages)) {
                for (var i = 0; i < carriages.Length; i++) {
                    var carriage = carriages[i];
                    if (carriage.m_Prefab != Entity.Null) {
                        perUnit += perCar(carriage.m_Prefab) * carriage.m_Count.x;
                    }
                }
            }

            return perUnit * GetUnitCount(prefab);
        }

        /// <summary>
        /// The countries the creator declared, as ISO codes joined by commas; empty when none.
        /// </summary>
        /// <remarks>
        /// Read off the vehicle's own variant component, placeholder or not. Sent as codes rather
        /// than a bitmask so the UI needs no copy of the enum: each code is also the flag file's
        /// name, so the UI turns "AT,CH" straight into two image URLs.
        /// </remarks>
        private string GetCountries(Entity prefab) {
            if (!EntityManager.TryGetComponent(prefab, out BTS_VariantData data) || data.m_Countries == 0) {
                return string.Empty;
            }

            var codes = new List<string>();
            foreach (Country c in System.Enum.GetValues(typeof(Country))) {
                if (c != Country.None && (data.m_Countries & (ulong)c) != 0) {
                    var code = CountryCodes.Of(c);
                    if (code != null) {
                        codes.Add(code);
                    }
                }
            }

            return string.Join(",", codes);
        }

        /// <summary>Passenger capacity of the whole consist.</summary>
        private int GetConsistPassengers(Entity prefab) {
            return Mathf.RoundToInt(SumConsist(prefab, car =>
                EntityManager.TryGetComponent(car, out PublicTransportVehicleData d) ? d.m_PassengerCapacity : 0));
        }

        /// <summary>
        /// Passenger-carrying cars in the consist, head car included, or 0 for a single unit with
        /// no carriage list.
        /// </summary>
        /// <remarks>
        /// Counts the head car so the figure matches how creators write it: the ICE 4 above is
        /// "1x12" in its name, and 1 head + 11 carriages is 12.
        ///
        /// Only cars with a passenger capacity count, at the creators' request: a consist list
        /// can carry prefabs that are not cars at all -- a wheelset or bogie built as a "carriage"
        /// so it articulates -- and counting those made a 3-car unit read as 5. The cost is that a
        /// locomotive (capacity 0) is not counted either, so a loco-hauled set reads as its
        /// coaches; that matches how such sets are named ("BR146 &amp; DOSTO (5)") and was the
        /// figure asked for.
        /// </remarks>
        private int GetConsistCars(Entity prefab) {
            if (!EntityManager.TryGetBuffer(prefab, true, out DynamicBuffer<VehicleCarriageElement> carriages)
                || carriages.Length == 0) {
                return 0;
            }

            var cars = CarriesPassengers(prefab) ? 1 : 0;
            for (var i = 0; i < carriages.Length; i++) {
                if (CarriesPassengers(carriages[i].m_Prefab)) {
                    cars += carriages[i].m_Count.x;
                }
            }

            return cars * GetUnitCount(prefab);
        }

        private bool CarriesPassengers(Entity car) =>
            car != Entity.Null
            && EntityManager.TryGetComponent(car, out PublicTransportVehicleData d)
            && d.m_PassengerCapacity > 0;

        /// <summary>
        /// Length of the whole consist in whole metres, from each car's own geometry.
        /// </summary>
        /// <remarks>
        /// <c>ObjectGeometryData.m_Size.z</c> is the long axis for a vehicle, which is how the game
        /// itself measures them — no separate length field exists. Summed over the consist for the
        /// same reason as capacity; the ICE 4 above comes to ~345m against the creator's 346m.
        /// </remarks>
        private int GetConsistLength(Entity prefab) {
            return Mathf.RoundToInt(SumConsist(prefab, car =>
                EntityManager.TryGetComponent(car, out ObjectGeometryData g) ? g.m_Size.z : 0f));
        }

        /// <summary>
        /// Resolves a prefab's top speed in km/h.
        /// </summary>
        /// <remarks>
        /// Speed lives on a different component per mode, and there is no shared base — this mirrors
        /// the order the game's own developer info panel uses, including the two details that are
        /// easy to get wrong: aircraft speed is on <c>AirplaneData</c>/<c>HelicopterData</c> (not on
        /// AircraftData, which exists but does not carry it), and the stored value is m/s, which the
        /// game converts for display by multiplying by 3.6.
        /// </remarks>
        /// <param name="prefab">The vehicle prefab entity.</param>
        /// <returns>Top speed in km/h, or 0 when no known component carries one.</returns>
        private int GetMaxSpeedKph(Entity prefab) {
            float speed;

            if (EntityManager.TryGetComponent(prefab, out CarData carData)) {
                speed = carData.m_MaxSpeed;
            } else if (EntityManager.TryGetComponent(prefab, out TrainData trainData)) {
                speed = trainData.m_MaxSpeed;
            } else if (EntityManager.TryGetComponent(prefab, out WatercraftData watercraftData)) {
                speed = watercraftData.m_MaxSpeed;
            } else if (EntityManager.TryGetComponent(prefab, out AirplaneData airplaneData)) {
                speed = airplaneData.m_FlyingSpeed.y;
            } else if (EntityManager.TryGetComponent(prefab, out HelicopterData helicopterData)) {
                speed = helicopterData.m_FlyingMaxSpeed;
            } else {
                return 0;
            }

            return Mathf.RoundToInt(speed * 3.6f);
        }
    }
}
