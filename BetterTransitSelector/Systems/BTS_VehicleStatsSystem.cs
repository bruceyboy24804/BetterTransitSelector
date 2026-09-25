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

    public partial class BTS_VehicleStatsSystem : CommonUISystemBase {
        protected override string ModId => Mod.Instance.Id;

        private EntityQuery m_VehiclePrefabQuery;

        private EntityQuery m_PackagedPrefabQuery;

        private EntityQuery m_VariantQuery;

        private EntityQuery m_PackQuery;

        private ModsCommon.Extensions.ValueBindingHelper<VehicleStats[]> m_Stats;

        private ModsCommon.Extensions.ValueBindingHelper<string[]> m_FavouritesBinding;

        private readonly Favourites m_Favourites = new Favourites("Favourites.json");

        private ModsCommon.Extensions.ValueBindingHelper<string[]> m_FavouritePacksBinding;

        private readonly Favourites m_FavouritePacks = new Favourites("FavouritePacks.json");

        private ModsCommon.Extensions.ValueBindingHelper<string[]> m_RecentBinding;

        private readonly RecentVehicles m_Recent = new RecentVehicles();

        private PrefabSystem m_PrefabSystem;

        private Dictionary<string, string> m_PackageTitles = new Dictionary<string, string>();

        private readonly Dictionary<string, string> m_PackageDates = new Dictionary<string, string>();

        private readonly Dictionary<string, string> m_PackageAuthors = new Dictionary<string, string>();

        private readonly Dictionary<string, Newtonsoft.Json.Linq.JToken> m_PackageItems = new Dictionary<string, Newtonsoft.Json.Linq.JToken>();

        private ModsCommon.Extensions.ValueBindingHelper<PackInfo> m_PackInfo;

        private ModsCommon.Extensions.ValueBindingHelper<AssetInfo> m_AssetInfo;

        private bool m_Dirty;

        private readonly HashSet<Entity> m_WarnedLegacyPacks = new HashSet<Entity>();

        protected override void OnCreate() {
            base.OnCreate();

            m_PrefabSystem  = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_PackageTitles = LoadPackageTitles();

            m_VehiclePrefabQuery = SystemAPI.QueryBuilder()
                                            .WithAll<PublicTransportVehicleData, PrefabData>()
                                            .Build();

            m_PackagedPrefabQuery = SystemAPI.QueryBuilder()
                                             .WithAll<ModPrerequisiteData, PrefabData>()
                                             .Build();

            m_PackQuery = SystemAPI.QueryBuilder()
                                        .WithAll<BTS_PackData, PrefabData>()
                                        .Build();

            m_VariantQuery = SystemAPI.QueryBuilder()
                                           .WithAll<BTS_VariantData, PrefabData>()
                                           .Build();

            m_Stats = CreateBinding("vehicleStats", new VehicleStats[0]);

            CreateBinding("statOptions", () => new StatOptions((Setting)Mod.Instance.Settings));

            CreateTrigger<string>("setSorting", name => {
                if (System.Enum.TryParse<Setting.SortOrder>(name, out var order)) {
                    var setting = (Setting)Mod.Instance.Settings;
                    setting.Sorting = order;
                    setting.ApplyAndSave();
                }
            });

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

            m_PackInfo = CreateBinding("packInfo", new PackInfo());
            CreateTrigger<string>("requestPackInfo", group => {
                try {
                    m_PackInfo.Value = BuildPackInfo(group);
                } catch (System.Exception e) {
                    m_Log.Warn($"requestPackInfo({group}) -- {e.Message}");
                    m_PackInfo.Value = new PackInfo { Group = group ?? string.Empty };
                }
            });

            m_AssetInfo = CreateBinding("assetInfo", new AssetInfo());
            CreateTrigger<string>("requestAssetInfo", id => {
                try {
                    m_AssetInfo.Value = BuildAssetInfo(id);
                } catch (System.Exception e) {
                    m_Log.Warn($"requestAssetInfo({id}) -- {e.Message}");
                    m_AssetInfo.Value = new AssetInfo { Id = id ?? string.Empty };
                }
            });

            CreateTrigger<string>("openUrl", url => {
                if (!string.IsNullOrEmpty(url) && (url.StartsWith("https://") || url.StartsWith("http://"))) {
                    Application.OpenURL(url);
                }
            });

            CreateTrigger<bool>("setRecentOpen", open => {
                var setting = (Setting)Mod.Instance.Settings;
                setting.RecentOpen = open;
                setting.ApplyAndSave();
            });

            m_Recent.Load();
            m_RecentBinding = CreateBinding("recent", m_Recent.Names.ToArray());
            CreateTrigger<string>("recordUsed", name => {
                m_Recent.Record(name);
                m_RecentBinding.Value = m_Recent.Names.ToArray();
            });
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode) {
            base.OnGameLoadingComplete(purpose, mode);

            RegisterPackTemplate();
            FileEditorCategories();

            m_Dirty = true;
        }

        protected override void OnUpdate() {
            if (m_Dirty && !m_VehiclePrefabQuery.IsEmptyIgnoreFilter) {
                RebuildStats();
                m_Dirty = false;
            }

            base.OnUpdate();
        }

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

                var group = GetGroup(entity);

                var title = GetPackageName(entity);
                if (string.IsNullOrEmpty(title)) {
                    titles.TryGetValue(GetGroupEntity(entity), out title);
                }

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

            foreach (var pair in titles) {
                m_Log.Debug($"RebuildStats() -- headline '{m_PrefabSystem.GetPrefabName(pair.Key)}' -> '{pair.Value}'");
            }
        }

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

                    m_PackageItems[id] = item;

                    var title = (string)item["displayName"];
                    if (!string.IsNullOrEmpty(title)) {
                        titles[id] = title;
                    }

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
                m_Log.Warn($"LoadPackageTitles() -- could not read the mods cache: {e.Message}");
            }

            return titles;
        }

        private AssetInfo BuildAssetInfo(string id) {
            var info = new AssetInfo { Id = id ?? string.Empty };

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

        private PackInfo BuildPackInfo(string group) {
            var info = new PackInfo { Group = group ?? string.Empty };
            if (string.IsNullOrEmpty(group) || !group.StartsWith("Mod:")) {
                return info;
            }

            var id = group.Substring(4);
            if (!m_PackageItems.TryGetValue(id, out var item)) {
                return info;
            }

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
                        info.Dependencies.Add((depId, (string)dep["displayName"] ?? depId, m_PackageItems.ContainsKey(depId)));
                    }
                }
            }

            return info;
        }

        private string GetPackageName(Entity prefab) {
            if (!EntityManager.TryGetComponent(prefab, out PrefabData prefabData)
                || !m_PrefabSystem.TryGetPrefab(prefabData, out PrefabBase prefabBase)
                || prefabBase.asset == null) {
                return string.Empty;
            }

            var meta = prefabBase.asset.GetMeta();

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

        private static bool IsHex(string value, int start) {
            for (var i = start; i < value.Length; i++) {
                var c = value[i];
                if ((c < '0' || c > '9') && (c < 'a' || c > 'f') && (c < 'A' || c > 'F')) {
                    return false;
                }
            }

            return true;
        }

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

        private const string PackTemplateName = "BTS Pack";

        private const string CategoryTemplate = "Better Transit Selector/Template";
        private const string CategoryMine     = "Better Transit Selector/My Packs";
        private const string CategoryShared   = "Better Transit Selector/Shared Packs";

        private bool m_TemplateRegistered;

        private void RegisterPackTemplate() {
            if (m_TemplateRegistered) {
                return;
            }

            var template  = ScriptableObject.CreateInstance<GroupPrefab>();
            template.name = PackTemplateName;
            template.AddComponent<BTS_Pack>();

            var category = template.AddComponent<EditorAssetCategoryOverride>();
            category.m_IncludeCategories = new[] { CategoryTemplate };

            template.AddComponent<UIObject>();

            if (m_PrefabSystem.AddPrefab(template)) {
                m_TemplateRegistered = true;
            } else {
                m_Log.Warn($"RegisterPackTemplate() -- AddPrefab refused '{PackTemplateName}'.");
            }
        }

        private void FileEditorCategories() {
            var packs  = m_PackQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var shared = SharedPrefabs.Folder.Replace('\\', '/');
            var seen   = new Dictionary<string, string>();

            for (var i = 0; i < packs.Length; i++) {
                if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(packs[i], out var prefabBase)) {
                    continue;
                }

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
            }

            packs.Dispose();

            World.GetExistingSystemManaged<Game.UI.Editor.EditorAssetCategorySystem>()
                 ?.SetMemberValue("m_Dirty", true);
        }

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

        private string GetRowIcon(Entity prefab) =>
            m_PrefabSystem.TryGetPrefab<PrefabBase>(prefab, out var prefabBase)
            && prefabBase.TryGet<BTS_Variant>(out var variant)
            && !string.IsNullOrEmpty(variant.m_Icon)
                ? UIExtensions.ResolveUri(variant.m_Icon)
                : string.Empty;

        private string GetIcon(Entity prefab) =>
            m_PrefabSystem.TryGetPrefab<PrefabBase>(prefab, out var prefabBase)
                ? ImageSystem.GetIcon(prefabBase) ?? string.Empty
                : string.Empty;

        private string GetUploadIcon(Entity pack) {
            if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(pack, out var prefabBase)) {
                return string.Empty;
            }

            if (prefabBase.TryGet<BTS_Pack>(out var component) && !string.IsNullOrEmpty(component.m_UploadIcon)) {
                return component.m_UploadIcon;
            }

            return string.Empty;
        }

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

#pragma warning disable CS0618
                if (m_PrefabSystem.TryGetPrefab<PrefabBase>(data.m_Placeholder, out var placeholderPrefab)
                    && placeholderPrefab is BTS_TrainPackPrefab
                    && m_WarnedLegacyPacks.Add(data.m_Placeholder)) {
#pragma warning restore CS0618
                    var pkg = placeholderPrefab.asset != null ? placeholderPrefab.asset.GetMeta().packageName : "(local)";
                    m_Log.Warn($"BuildVariantFamilies() -- '{placeholderPrefab.name}' in {pkg} is an old-style pack (BTS_TrainPackPrefab). It fails to load for players with the mod disabled; recreate it from the BTS Pack template and republish.");
                }

                var title = m_PrefabSystem.GetPrefabName(data.m_Placeholder) ?? string.Empty;

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

        private const string CollectionPrefix = "Col:";

        private const string HeadingPrefix = "Heading:";

        private BTS_Pack GetPack(Entity placeholder) =>
            m_PrefabSystem.TryGetPrefab<PrefabBase>(placeholder, out var prefabBase) && prefabBase.TryGet<BTS_Pack>(out var pack)
                ? pack
                : null;

        private static string NormaliseHeading(string name) =>
            System.Text.RegularExpressions.Regex.Replace((name ?? string.Empty).Trim(), @"\s+", " ").ToLowerInvariant();

        private void CanonicaliseHeadings(Dictionary<Entity, Variant> variants) {
            var oldest = new Dictionary<string, (string date, string title)>();
            var icons  = new Dictionary<string, (string date, string icon)>();

            foreach (var pair in variants) {
                var v = pair.Value;
                if (string.IsNullOrEmpty(v.Collection) || !v.Collection.StartsWith(HeadingPrefix)) {
                    continue;
                }

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

        private string GetAssetGuid(Entity prefab) {
            if (!EntityManager.TryGetComponent(prefab, out PrefabData prefabData)
                || !m_PrefabSystem.TryGetPrefab(prefabData, out PrefabBase prefabBase)
                || prefabBase.asset == null) {
                return string.Empty;
            }

            return prefabBase.asset.id.guid.ToString();
        }

        private Dictionary<Entity, string> BuildGroupTitles() {
            var entities   = m_PackagedPrefabQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var titles     = new Dictionary<Entity, string>();
            var packHeaded = new HashSet<Entity>();

            for (var i = 0; i < entities.Length; i++) {
                var entity = entities[i];

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

        private Entity GetGroupEntity(Entity prefab) {
            return EntityManager.TryGetComponent(prefab, out ModPrerequisiteData mod)
                ? mod.m_ContentPrerequisite
                : Entity.Null;
        }

        private string GetGroup(Entity prefab) {
            if (EntityManager.TryGetComponent(prefab, out ModPrerequisiteData mod)
                && mod.m_ContentPrerequisite != Entity.Null) {
                return m_PrefabSystem.GetPrefabName(mod.m_ContentPrerequisite) ?? string.Empty;
            }

            if (EntityManager.TryGetBuffer(prefab, true, out DynamicBuffer<AssetPackElement> packs)
                && packs.Length > 0) {
                return m_PrefabSystem.GetPrefabName(packs[0].m_Pack) ?? string.Empty;
            }

            return string.Empty;
        }

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

        private string GetEnergyType(Entity prefab) {
            if (EntityManager.TryGetComponent(prefab, out CarData car)) {
                return car.m_EnergyType.ToString();
            }

            if (EntityManager.TryGetComponent(prefab, out TrainData train)) {
                return train.m_EnergyType.ToString();
            }

            return string.Empty;
        }

        private int GetUnitCount(Entity prefab) =>
            EntityManager.HasComponent<MultipleUnitTrainData>(prefab)
            && EntityManager.TryGetComponent(prefab, out TrainEngineData engine)
            && engine.m_Count.x > 0
                ? engine.m_Count.x
                : 1;

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

        private int GetConsistPassengers(Entity prefab) {
            return Mathf.RoundToInt(SumConsist(prefab, car =>
                EntityManager.TryGetComponent(car, out PublicTransportVehicleData d) ? d.m_PassengerCapacity : 0));
        }

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

        private int GetConsistLength(Entity prefab) {
            return Mathf.RoundToInt(SumConsist(prefab, car =>
                EntityManager.TryGetComponent(car, out ObjectGeometryData g) ? g.m_Size.z : 0f));
        }

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
