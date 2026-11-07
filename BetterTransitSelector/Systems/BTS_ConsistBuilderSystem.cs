namespace BetterTransitSelector.Systems {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Colossal.Entities;
    using Colossal.Serialization.Entities;

    using Game;
    using Game.Net;
    using Game.Prefabs;
    using Game.Routes;
    using Game.UI;
    using Game.UI.InGame;
    using Game.Vehicles;

    using BetterTransitSelector.Components;
    using BetterTransitSelector.Domain;

    using ModsCommon.Extensions;
    using ModsCommon.Systems;

    using Unity.Collections;
    using Unity.Entities;

    #endregion

    /// <summary>
    /// Backs the train builder: edits a draft train, keeps the player's saved trains, and applies one to the
    /// selected line when it is picked in the vehicle dropdown.
    /// </summary>
    /// <remarks>
    /// Editing never touches the line: its <see cref="BTS_ConsistCar"/> buffer is written only by the apply
    /// triggers, as a copy, so later edits to a saved train leave lines alone until it is picked again. Model
    /// selection is never written here; the UI sends each row's models through vanilla's <c>selectVehicles</c>.
    /// </remarks>
    public partial class BTS_ConsistBuilderSystem : CommonUISystemBase {
        protected override string ModId => Mod.Instance.Id;

        private SelectedInfoUISystem m_SelectedInfo;
        private PrefabSystem         m_PrefabSystem;
        private ImageSystem          m_ImageSystem;
        private NameSystem           m_NameSystem;

        /// <summary>Source of each car's pack title and author, which it already resolves for the vehicle list.</summary>
        private BTS_VehicleStatsSystem m_Stats;

        private BTS_CustomCarSystem m_CustomCars;

        private EntityQuery m_CarQuery;

        /// <summary>Every installed car, locked or not: a saved train may name one still locked in this city.</summary>
        private EntityQuery m_AllCarsQuery;

        private readonly SavedConsistStore m_Library = new SavedConsistStore();

        private readonly List<BTS_ConsistCar> m_Draft = new List<BTS_ConsistCar>();
        private Entity m_DraftLine;
        private string m_EditingId = string.Empty;
        private int    m_DraftVersion;

        /// <summary>The draft train, front first. Never saved with the game.</summary>
        public IReadOnlyList<BTS_ConsistCar> DraftCars => m_Draft;

        private ValueBindingHelper<ConsistBuilder> m_Builder;

        /// <summary>Hash of everything the published value depends on; the value is rebuilt only when it moves.</summary>
        private int m_Signature;

        private List<ConsistCatalogueCar> m_Catalogue = new List<ConsistCatalogueCar>();
        private int                       m_CatalogueKey;

        protected override void OnCreate() {
            base.OnCreate();
            m_SelectedInfo = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ImageSystem  = World.GetOrCreateSystemManaged<ImageSystem>();
            m_NameSystem   = World.GetOrCreateSystemManaged<NameSystem>();
            m_Stats        = World.GetOrCreateSystemManaged<BTS_VehicleStatsSystem>();
            m_CustomCars   = World.GetOrCreateSystemManaged<BTS_CustomCarSystem>();

            // Naming PrefabData drops obsolete prefabs; WithNone on the enableable Locked keeps only cars
            // the city has unlocked (Locked disabled or absent).
            m_CarQuery = SystemAPI.QueryBuilder()
                                  .WithAll<TrainData, ObjectData, PrefabData>()
                                  .WithNone<Locked>()
                                  .Build();

            m_AllCarsQuery = SystemAPI.QueryBuilder()
                                      .WithAll<TrainData, ObjectData, PrefabData>()
                                      .Build();

            m_Library.Load();

            m_Builder = CreateBinding("consistBuilder", ConsistBuilder.Empty);

            // Every edit addresses a block of cars as (start, count); a single car is a block of one. Positions
            // index the built train itself, never the catalogue, whose order changes with sorting and filters.
            CreateTrigger<Entity, int>("consistInsert", (prefab, index) => Edit(cars => {
                if (IsCatalogueCar(prefab)) {
                    cars.Insert(index < 0 || index > cars.Count ? cars.Count : index, new BTS_ConsistCar(prefab, false));
                }
            }));
            CreateTrigger<int, int, int>("consistRepeat", (start, count, times) => Edit(cars => {
                if (ValidBlock(cars, start, count) && times > 0) {
                    var block = cars.GetRange(start, count);
                    for (var i = 0; i < times; i++) {
                        cars.InsertRange(start + count, block);
                    }
                }
            }));
            CreateTrigger<int, int>("consistRemove", (start, count) => Edit(cars => {
                if (ValidBlock(cars, start, count)) {
                    cars.RemoveRange(start, count);
                }
            }));
            CreateTrigger<int, int, int>("consistMove", (start, count, to) => Edit(cars => {
                if (ValidBlock(cars, start, count) && to >= 0 && to + count <= cars.Count) {
                    var block = cars.GetRange(start, count);
                    cars.RemoveRange(start, count);
                    cars.InsertRange(to, block);
                }
            }));
            CreateTrigger<int, int>("consistFlip", (start, count) => Edit(cars => {
                if (ValidBlock(cars, start, count)) {
                    TurnBlock(cars, start, count);
                }
            }));
            // Changes one figure on a block of cars: "passengers", "cargo", "maxSpeed" (km/h), "acceleration" or
            // "braking" (m/s²); 0 or less puts it back to the asset's. Each car is switched to the custom car
            // (a prefab of its own) with its figures, or back to its asset once none is changed.
            CreateTrigger<int, int, string, float>("consistSetStat", (start, count, stat, value) => Edit(cars => {
                if (!ValidBlock(cars, start, count)) {
                    return;
                }

                for (var i = start; i < start + count; i++) {
                    var car = cars[i];
                    if (!m_CustomCars.TryDescribe(car.m_Prefab, out var original, out var stats)) {
                        original = car.m_Prefab;
                        stats    = default;
                    }

                    if (!stats.Set(stat, value)) {
                        return;
                    }

                    var prefab = m_CustomCars.GetOrCreate(original, stats);
                    if (prefab != Entity.Null) {
                        car.m_Prefab = prefab;
                        cars[i]      = car;
                    }
                }
            }));
            CreateTrigger("consistTurnAll", () => Edit(cars => TurnBlock(cars, 0, cars.Count)));
            CreateTrigger("consistMirror", () => Edit(MirrorFront));
            CreateTrigger<Entity>("consistLoadRecipe", prefab => Edit(cars => LoadRecipe(cars, prefab)));
            CreateTrigger("consistClear", () => Edit(cars => cars.Clear()));
            CreateTrigger("consistCopyRunning", () => Edit(CopyRunningTrain));

            CreateTrigger<string>("consistConfigSaveNew", name => {
                SelectedRailLine(out var track, out _);
                if (TryDraftCars(out var saved)) {
                    m_EditingId = m_Library.Add(name, track, saved).Id;
                }
            });
            CreateTrigger("consistConfigSave", () => {
                SelectedRailLine(out var track, out _);
                if (m_EditingId != string.Empty && TryDraftCars(out var saved)) {
                    m_Library.Replace(m_EditingId, track, saved);
                }
            });
            CreateTrigger<string, string>("consistConfigRename", (id, name) => m_Library.Rename(id, name));
            CreateTrigger<string>("consistConfigDelete", id => {
                // The draft stays, as an unsaved train; lines using a copy of it are unaffected.
                m_Library.Delete(id);
                if (m_EditingId == id) {
                    m_EditingId = string.Empty;
                }
            });
            CreateTrigger<string>("consistConfigOpen", id => {
                if (m_Library.TryGet(id, out var config)) {
                    Resolve(config, CarsByName(), m_Draft);
                    m_EditingId = id;
                    m_DraftVersion++;
                } else if (string.IsNullOrEmpty(id)) {
                    // "New train": an empty draft.
                    m_EditingId = string.Empty;
                    m_Draft.Clear();
                    m_DraftVersion++;
                }
            });

            CreateTrigger<string>("consistApply", id => {
                var line = SelectedRailLine(out _, out _);
                if (line == Entity.Null || !m_Library.TryGet(id, out var config)) {
                    return;
                }

                var cars = new List<BTS_ConsistCar>();
                if (Resolve(config, CarsByName(), cars)) {
                    WriteLine(line, cars);
                }
            });
            CreateTrigger("consistApplyDefault", () => {
                var line = SelectedRailLine(out _, out _);
                if (line != Entity.Null) {
                    WriteLine(line, new List<BTS_ConsistCar>());
                }
            });

            // Rolling refit: BTS_RefitSystem retires the line's mismatched trains one at a time.
            CreateTrigger("consistRefit", () => {
                var line = SelectedRailLine(out _, out _);
                var em   = EntityManager;
                if (line == Entity.Null || !em.TryGetBuffer(line, true, out DynamicBuffer<BTS_ConsistCar> build) || build.Length == 0) {
                    return;
                }

                using var cars = build.ToNativeArray(Allocator.Temp);
                var count = ConsistRules.CountMismatched(em, line, cars);
                if (count > 0) {
                    em.AddComponentData(line, new BTS_Refit { m_Train = Entity.Null, m_Remaining = count });
                }
            });
            CreateTrigger("consistRefitStop", () => {
                // Stopping leaves a train already told to leave to finish leaving; only the next ones are spared.
                var line = SelectedRailLine(out _, out _);
                if (line != Entity.Null && EntityManager.HasComponent<BTS_Refit>(line)) {
                    EntityManager.RemoveComponent<BTS_Refit>(line);
                }
            });
        }

        protected override void OnUpdate() {
            var line      = SelectedRailLine(out var track, out var lineData);
            if (line != m_DraftLine) {
                m_DraftLine = line;
                m_EditingId = string.Empty;
                LoadDraft(line);
            }

            var signature = Signature(line);
            if (signature != m_Signature) {
                m_Signature      = signature;
                m_Builder.Value = line == Entity.Null ? ConsistBuilder.Empty : Build(line, track, lineData);
            }

            base.OnUpdate();
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode) {
            base.OnGamePreload(purpose, mode);
            // The draft holds this world's entities; a load may reuse them for something else.
            m_DraftLine = Entity.Null;
            m_EditingId = string.Empty;
            m_Draft.Clear();
            m_DraftVersion++;
        }

        #region Publishing

        private ConsistBuilder Build(Entity line, TrackTypes track, TransportLineData lineData) {
            var em      = EntityManager;
            var builder = new ConsistBuilder {
                Valid     = true,
                LineName  = m_NameSystem.GetRenderedLabelName(line) ?? string.Empty,
                Catalogue = Catalogue(track, lineData),
            };

            em.TryGetBuffer(line, true, out DynamicBuffer<VehicleModel> models);
            em.TryGetBuffer(line, true, out DynamicBuffer<BTS_ConsistCar> lineCars);
            FillRelated(builder.Related, models, m_Draft);

            builder.Refitting = em.HasComponent<BTS_Refit>(line);
            if (lineCars.IsCreated && lineCars.Length > 0) {
                using var running = lineCars.ToNativeArray(Allocator.Temp);
                builder.Mismatched  = ConsistRules.CountMismatched(em, line, running);
                builder.OtherModels = models.IsCreated && HasOtherPrimary(models, lineCars);
            }

            if (models.IsCreated) {
                for (var i = 0; i < models.Length; i++) {
                    foreach (var model in new[] { models[i].m_PrimaryPrefab, models[i].m_SecondaryPrefab }) {
                        if (model != Entity.Null && !builder.Models.Contains(model)) {
                            builder.Models.Add(model);
                        }
                    }
                }
            }

            if (m_Library.TryGet(m_EditingId, out var editing)) {
                builder.EditingId   = editing.Id;
                builder.EditingName = editing.Name;
            }

            FillConfigs(builder, track, lineData, models, lineCars);

            var lead = FirstLead(m_Draft, models);
            for (var i = 0; i < m_Draft.Count; i++) {
                var prefab = m_Draft[i].m_Prefab;
                // A custom car reports its asset too, so the UI can name it and show which figures changed.
                var custom  = m_CustomCars.TryDescribe(prefab, out var original, out _);
                var figures = FiguresOf(prefab);
                builder.Cars.Add(new ConsistSlot(
                    prefab, NameOf(prefab), m_Draft[i].m_Reversed, !IsCatalogueCar(prefab), i == lead,
                    custom ? NameOf(original) : string.Empty,
                    KindOf(prefab),
                    LengthOf(prefab),
                    ThumbnailOf(custom ? original : prefab),
                    figures,
                    custom ? FiguresOf(original) : figures));
            }

            if (m_Draft.Count > 0) {
                builder.Status = lead < 0 ? "NoLead"
                    : !em.HasComponent<MultipleUnitTrainData>(m_Draft[lead].m_Prefab) && !m_Draft.Any(c => IsCatalogueCar(c.m_Prefab) && ConsistRules.IsSecondary(em, c.m_Prefab)) ? "NoEngine"
                    : "Ok";
            }

            return builder;
        }

        /// <summary>The first car that can lead, preferring one the line already accepts. -1 when none can.</summary>
        private int FirstLead(List<BTS_ConsistCar> cars, DynamicBuffer<VehicleModel> models) {
            var em    = EntityManager;
            var first = -1;
            for (var i = 0; i < cars.Count; i++) {
                var prefab = cars[i].m_Prefab;
                if (!IsCatalogueCar(prefab) || !ConsistRules.IsPrimary(em, prefab)) {
                    continue;
                }

                if (models.IsCreated && ConsistRules.PrimaryAccepted(models, prefab)) {
                    return i;
                }

                if (first < 0) {
                    first = i;
                }
            }

            return first;
        }

        /// <summary>Lists the saved trains this line can run and works out which one, if any, it is running.</summary>
        private void FillConfigs(ConsistBuilder builder, TrackTypes track, TransportLineData lineData,
                                 DynamicBuffer<VehicleModel> models, DynamicBuffer<BTS_ConsistCar> lineCars) {
            var em      = EntityManager;
            var byName  = CarsByName();
            var cars    = new List<BTS_ConsistCar>();
            var running = lineCars.IsCreated && lineCars.Length > 0;
            builder.LineState = running ? "Modified" : "Default";

            foreach (var config in m_Library.All) {
                if (config.Track != track) {
                    continue;
                }

                var complete = Resolve(config, byName, cars);
                // Same rule as the catalogue: a passenger line has no use for freight cars, and the other way round.
                var fits = true;
                var row  = new ConsistConfigRow { Id = config.Id, Name = config.Name, Cars = cars.Count, Missing = !complete };
                var low  = float.MaxValue;
                foreach (var car in cars) {
                    var prefab  = car.m_Prefab;
                    var figures = FiguresOf(prefab);
                    fits &= !(!lineData.m_PassengerTransport && em.HasComponent<PublicTransportVehicleData>(prefab))
                            && !(!lineData.m_CargoTransport && em.HasComponent<CargoTransportVehicleData>(prefab));
                    row.Passengers += figures.Passengers;
                    row.Cargo      += figures.Cargo;
                    if (figures.MaxSpeed > 0f) {
                        low = Math.Min(low, figures.MaxSpeed);
                    }
                }

                if (!fits) {
                    continue;
                }

                row.MaxSpeed = low == float.MaxValue ? 0f : low;

                var lead = FirstLead(cars, models);
                row.CanLead = lead >= 0;
                if (lead >= 0) {
                    row.Lead = cars[lead].m_Prefab;
                    if (models.IsCreated && ConsistRules.HasSecondary(models) && !em.HasComponent<MultipleUnitTrainData>(row.Lead)) {
                        var accepted = false;
                        var engine   = Entity.Null;
                        for (var i = 0; i < cars.Count; i++) {
                            var prefab = cars[i].m_Prefab;
                            accepted |= ConsistRules.SecondaryAccepted(models, prefab);
                            if (engine == Entity.Null && IsCatalogueCar(prefab) && ConsistRules.IsSecondary(em, prefab)) {
                                engine = prefab;
                            }
                        }

                        row.Engine = accepted ? Entity.Null : engine;
                    }
                }

                row.Thumbnail = ThumbnailOf(m_CustomCars.TryDescribe(row.Lead, out var original, out _) ? original : row.Lead);
                row.Selected  = running && SameTrain(lineCars, cars);
                if (row.Selected) {
                    builder.LineState = "Config";
                }

                builder.Configs.Add(row);
            }
        }

        private static bool SameTrain(DynamicBuffer<BTS_ConsistCar> a, List<BTS_ConsistCar> b) {
            if (a.Length != b.Count) {
                return false;
            }

            for (var i = 0; i < a.Length; i++) {
                if (a[i].m_Prefab != b[i].m_Prefab || a[i].m_Reversed != b[i].m_Reversed) {
                    return false;
                }
            }

            return true;
        }

        private Dictionary<string, Entity> CarsByName() {
            var map = new Dictionary<string, Entity>();
            using var entities = m_AllCarsQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities) {
                map[m_PrefabSystem.GetPrefabName(entity)] = entity;
            }

            return map;
        }

        /// <summary>
        /// Fills <paramref name="cars"/> with a saved train's cars. A car whose asset is gone becomes a null slot.
        /// </summary>
        /// <returns>False when any car is missing.</returns>
        private static bool Resolve(SavedConsist config, Dictionary<string, Entity> byName, List<BTS_ConsistCar> cars) {
            cars.Clear();
            var complete = true;
            foreach (var car in config.Cars) {
                complete &= byName.TryGetValue(car.Prefab, out var prefab);
                cars.Add(new BTS_ConsistCar(prefab, car.Reversed));
            }

            return complete;
        }

        private List<ConsistCatalogueCar> Catalogue(TrackTypes track, TransportLineData lineData) {
            var count = m_CarQuery.CalculateEntityCount();
            var key   = unchecked(((int)track * 397 + count) * 4 + (lineData.m_PassengerTransport ? 1 : 0) + (lineData.m_CargoTransport ? 2 : 0));
            if (key == m_CatalogueKey && m_Catalogue.Count > 0) {
                return m_Catalogue;
            }

            var em   = EntityManager;
            var list = new List<ConsistCatalogueCar>();
            using var entities = m_CarQuery.ToEntityArray(Allocator.Temp);
            foreach (var prefab in entities) {
                if (em.GetComponentData<TrainData>(prefab).m_TrackType != track) {
                    continue;
                }

                // A passenger line has no use for freight cars, and the other way round; locomotives and
                // tenders carry neither and fit both.
                if ((!lineData.m_PassengerTransport && em.HasComponent<PublicTransportVehicleData>(prefab))
                    || (!lineData.m_CargoTransport && em.HasComponent<CargoTransportVehicleData>(prefab))) {
                    continue;
                }

                // Custom cars are made in the train, from their asset; listing them would bury the assets.
                var name = NameOf(prefab);
                if (CustomCar.OriginalOf(name) != null) {
                    continue;
                }

                list.Add(new ConsistCatalogueCar(
                    prefab,
                    name,
                    KindOf(prefab),
                    LengthOf(prefab),
                    ThumbnailOf(prefab),
                    m_Stats?.GetPackageName(prefab) ?? string.Empty,
                    m_Stats?.GetAuthor(prefab) ?? string.Empty,
                    FiguresOf(prefab)));
            }

            m_Catalogue    = list.OrderBy(c => KindOrder(c.Kind)).ThenBy(c => c.Id).ToList();
            m_CatalogueKey = key;
            return m_Catalogue;
        }

        /// <summary>
        /// The line's selected models, every car their recipes (<see cref="VehicleCarriageElement"/>) name, and
        /// every car already in the built train, each once.
        /// </summary>
        private void FillRelated(List<Entity> related, DynamicBuffer<VehicleModel> models, List<BTS_ConsistCar> cars) {
            var seen = new HashSet<Entity>();
            void Add(Entity prefab) {
                if (prefab == Entity.Null || !seen.Add(prefab)) {
                    return;
                }

                related.Add(prefab);
                if (EntityManager.TryGetBuffer(prefab, true, out DynamicBuffer<VehicleCarriageElement> recipe)) {
                    for (var i = 0; i < recipe.Length; i++) {
                        Add(recipe[i].m_Prefab);
                    }
                }
            }

            if (models.IsCreated) {
                for (var i = 0; i < models.Length; i++) {
                    Add(models[i].m_PrimaryPrefab);
                    Add(models[i].m_SecondaryPrefab);
                }
            }

            foreach (var car in cars) {
                Add(car.m_Prefab);
            }
        }

        private string KindOf(Entity prefab) {
            var em = EntityManager;
            if (em.HasComponent<MultipleUnitTrainData>(prefab)) {
                return "Unit";
            }

            if (em.HasComponent<TrainEngineData>(prefab)) {
                return "Engine";
            }

            return em.HasComponent<TrainCarriageData>(prefab) ? "Carriage" : "Other";
        }

        private static int KindOrder(string kind) =>
            kind switch {
                "Engine"   => 0,
                "Unit"     => 1,
                "Carriage" => 2,
                _          => 3,
            };

        private int Signature(Entity line) {
            unchecked {
                var hash = line.Index * 31 + line.Version;
                if (line == Entity.Null) {
                    return hash;
                }

                var em = EntityManager;
                hash = hash * 31 + m_DraftVersion;
                hash = hash * 31 + m_Library.Version;
                hash = hash * 31 + m_EditingId.GetHashCode();
                foreach (var car in m_Draft) {
                    // A custom car's data fills in the frame after it is created; its speed moving from 0
                    // republishes the slot with real figures.
                    if (em.TryGetComponent(car.m_Prefab, out TrainData train)) {
                        hash = hash * 31 + train.m_MaxSpeed.GetHashCode();
                    }
                }

                if (em.TryGetBuffer(line, true, out DynamicBuffer<BTS_ConsistCar> cars)) {
                    hash = hash * 31 + cars.Length + 1;
                    for (var i = 0; i < cars.Length; i++) {
                        hash = hash * 31 + cars[i].m_Prefab.Index * 2 + (cars[i].m_Reversed ? 1 : 0);
                    }
                }

                if (em.TryGetBuffer(line, true, out DynamicBuffer<VehicleModel> models)) {
                    for (var i = 0; i < models.Length; i++) {
                        hash = hash * 31 + models[i].m_PrimaryPrefab.Index;
                        hash = hash * 31 + models[i].m_SecondaryPrefab.Index;
                    }
                }

                // Running trains are replaced as a refit goes: their count, and whether they still differ, move.
                hash = hash * 31 + (em.HasComponent<BTS_Refit>(line) ? 1 : 0);
                if (em.TryGetBuffer(line, true, out DynamicBuffer<BTS_ConsistCar> build) && build.Length > 0) {
                    using var buildCars = build.ToNativeArray(Allocator.Temp);
                    hash = hash * 31 + ConsistRules.CountMismatched(em, line, buildCars);
                }

                return hash * 31 + m_CarQuery.CalculateEntityCount();
            }
        }

        #endregion

        #region Editing

        /// <summary>Applies <paramref name="change"/> to the draft. The line itself is not touched.</summary>
        private void Edit(System.Action<List<BTS_ConsistCar>> change) {
            if (SelectedRailLine(out _, out _) == Entity.Null) {
                return;
            }

            change(m_Draft);
            m_DraftVersion++;
        }

        private void LoadDraft(Entity line) {
            m_Draft.Clear();
            if (line != Entity.Null && EntityManager.TryGetBuffer(line, true, out DynamicBuffer<BTS_ConsistCar> cars)) {
                for (var i = 0; i < cars.Length; i++) {
                    m_Draft.Add(cars[i]);
                }
            }

            m_DraftVersion++;
        }

        /// <summary>The draft by prefab name. False when it is empty or has a car whose asset is gone.</summary>
        private bool TryDraftCars(out List<SavedConsistCar> cars) {
            cars = new List<SavedConsistCar>(m_Draft.Count);
            foreach (var car in m_Draft) {
                if (!IsCatalogueCar(car.m_Prefab)) {
                    return false;
                }

                cars.Add(new SavedConsistCar { Prefab = NameOf(car.m_Prefab), Reversed = car.m_Reversed });
            }

            return cars.Count > 0;
        }

        /// <summary>
        /// Replaces the line's train. An empty one removes the buffer, so a line on the game default carries
        /// nothing of this mod in the save.
        /// </summary>
        private void WriteLine(Entity line, List<BTS_ConsistCar> cars) {
            var em = EntityManager;
            if (cars.Count == 0) {
                if (em.HasBuffer<BTS_ConsistCar>(line)) {
                    em.RemoveComponent<BTS_ConsistCar>(line);
                }

                return;
            }

            var buffer = em.HasBuffer<BTS_ConsistCar>(line) ? em.GetBuffer<BTS_ConsistCar>(line) : em.AddBuffer<BTS_ConsistCar>(line);
            buffer.Clear();
            foreach (var car in cars) {
                buffer.Add(car);
            }
        }

        /// <summary>
        /// Turns a block of cars round, as if it were lifted off and set back facing the other way: the order
        /// reverses and every car's direction flips. A block of one is simply turned.
        /// </summary>
        private static void TurnBlock(List<BTS_ConsistCar> cars, int start, int count) {
            cars.Reverse(start, count);
            for (var i = start; i < start + count; i++) {
                var car = cars[i];
                car.m_Reversed = !car.m_Reversed;
                cars[i]        = car;
            }
        }

        /// <summary>
        /// Puts the train's leading power at the back too, turned round (top-and-tail). The leading power is the
        /// run of engines and capacity-less cars (tenders) at the front; a train that starts with anything else,
        /// such as a multiple-unit cab, mirrors its first car.
        /// </summary>
        private void MirrorFront(List<BTS_ConsistCar> cars) {
            if (cars.Count == 0) {
                return;
            }

            var em   = EntityManager;
            var lead = 0;
            while (lead < cars.Count) {
                var prefab   = cars[lead].m_Prefab;
                var carrying = (em.TryGetComponent(prefab, out PublicTransportVehicleData p) && p.m_PassengerCapacity > 0)
                               || (em.TryGetComponent(prefab, out CargoTransportVehicleData c) && c.m_CargoCapacity > 0);
                if (!ConsistRules.IsSecondary(em, prefab) && carrying) {
                    break;
                }

                lead++;
            }

            var block = cars.GetRange(0, Math.Max(1, lead));
            var tail  = block.Count;
            cars.AddRange(block);
            TurnBlock(cars, cars.Count - tail, tail);
        }

        /// <summary>
        /// Replaces the built train with what vanilla would build from <paramref name="prefab"/>, one of the line's
        /// selected models. Follows <c>TransportVehicleSelectData.CreateVehicle</c>, which always takes each count's
        /// minimum: a multiple-unit model repeats its unit (front, its listed cars, the rear cab); a locomotive
        /// model gets its engines and tenders, then carriages of the line's first selected carriage model.
        /// </summary>
        private void LoadRecipe(List<BTS_ConsistCar> cars, Entity prefab) {
            var em   = EntityManager;
            var line = SelectedRailLine(out _, out _);
            if (!IsCatalogueCar(prefab) || !em.TryGetBuffer(line, true, out DynamicBuffer<VehicleModel> models)) {
                return;
            }

            var recipe = new List<BTS_ConsistCar>();
            var units  = em.TryGetComponent(prefab, out TrainEngineData engine) ? Math.Max(1, engine.m_Count.x) : 1;

            if (em.HasComponent<MultipleUnitTrainData>(prefab)) {
                for (var u = 0; u < units; u++) {
                    recipe.Add(new BTS_ConsistCar(prefab, false));
                    AppendListedCars(recipe, prefab, out _);
                }
            } else {
                // Engine and carriage are chosen separately in vanilla (secondary and primary); whichever one
                // was picked, the other comes from the line's selection.
                var locomotive = ConsistRules.IsSecondary(em, prefab) ? prefab : FirstSelected(models, secondary: true);
                var carriage   = ConsistRules.IsSecondary(em, prefab) ? FirstSelected(models, secondary: false) : prefab;
                if (locomotive == Entity.Null) {
                    return;
                }

                units = em.TryGetComponent(locomotive, out TrainEngineData locoData) ? Math.Max(1, locoData.m_Count.x) : 1;
                var carriages = 0;
                for (var u = 0; u < units; u++) {
                    recipe.Add(new BTS_ConsistCar(locomotive, false));
                    AppendListedCars(recipe, locomotive, out var open);
                    carriages += open;
                }

                if (carriage != Entity.Null) {
                    for (var i = 0; i < carriages; i++) {
                        recipe.Add(new BTS_ConsistCar(carriage, false));
                    }
                }
            }

            cars.Clear();
            cars.AddRange(recipe);
        }

        /// <summary>
        /// Adds the cars a prefab's <see cref="VehicleCarriageElement"/> buffer names, at their minimum count.
        /// Entries without a prefab are the slots vanilla fills with the line's carriage model; their total is
        /// returned in <paramref name="openSlots"/>.
        /// </summary>
        private void AppendListedCars(List<BTS_ConsistCar> recipe, Entity prefab, out int openSlots) {
            openSlots = 0;
            if (!EntityManager.TryGetBuffer(prefab, true, out DynamicBuffer<VehicleCarriageElement> listed)) {
                return;
            }

            for (var i = 0; i < listed.Length; i++) {
                var element = listed[i];
                if (element.m_Prefab == Entity.Null) {
                    openSlots += element.m_Count.x;
                    continue;
                }

                for (var n = 0; n < element.m_Count.x; n++) {
                    recipe.Add(new BTS_ConsistCar(element.m_Prefab, element.m_Direction == VehicleCarriageDirection.Reversed));
                }
            }
        }

        private static Entity FirstSelected(DynamicBuffer<VehicleModel> models, bool secondary) {
            for (var i = 0; i < models.Length; i++) {
                var prefab = secondary ? models[i].m_SecondaryPrefab : models[i].m_PrimaryPrefab;
                if (prefab != Entity.Null) {
                    return prefab;
                }
            }

            return Entity.Null;
        }

        /// <summary>Replaces the built train with the cars of a train running on the line, as a starting point.</summary>
        private void CopyRunningTrain(List<BTS_ConsistCar> cars) {
            var line = SelectedRailLine(out _, out _);
            var em   = EntityManager;
            if (!em.TryGetBuffer(line, true, out DynamicBuffer<RouteVehicle> vehicles)) {
                return;
            }

            for (var v = 0; v < vehicles.Length; v++) {
                if (!em.TryGetBuffer(vehicles[v].m_Vehicle, true, out DynamicBuffer<LayoutElement> layout) || layout.Length == 0) {
                    continue;
                }

                cars.Clear();
                for (var i = 0; i < layout.Length; i++) {
                    var car = layout[i].m_Vehicle;
                    if (em.TryGetComponent(car, out PrefabRef prefabRef) && em.TryGetComponent(car, out Train train)) {
                        cars.Add(new BTS_ConsistCar(prefabRef.m_Prefab, (train.m_Flags & Game.Vehicles.TrainFlags.Reversed) != 0));
                    }
                }

                return;
            }
        }

        #endregion

        #region Helpers

        /// <summary>The selected entity when it is a train, tram or subway line; otherwise <see cref="Entity.Null"/>.</summary>
        private Entity SelectedRailLine(out TrackTypes track, out TransportLineData lineData) {
            track    = TrackTypes.None;
            lineData = default;

            var line = m_SelectedInfo.selectedEntity;
            var em   = EntityManager;
            if (line == Entity.Null
                || !em.Exists(line)
                || !em.HasComponent<TransportLine>(line)
                || !em.TryGetComponent(line, out PrefabRef prefabRef)
                || !em.TryGetComponent(prefabRef.m_Prefab, out lineData)) {
                return Entity.Null;
            }

            track = ConsistRules.TrackOf(lineData.m_TransportType);
            return track == TrackTypes.None ? Entity.Null : line;
        }

        private bool IsCatalogueCar(Entity prefab) {
            var em = EntityManager;
            return prefab != Entity.Null
                   && em.Exists(prefab)
                   && em.HasComponent<PrefabData>(prefab)
                   && em.IsComponentEnabled<PrefabData>(prefab)
                   && em.HasComponent<TrainData>(prefab)
                   && em.HasComponent<ObjectData>(prefab);
        }

        private string NameOf(Entity prefab) =>
            prefab != Entity.Null && EntityManager.Exists(prefab) ? m_PrefabSystem.GetPrefabName(prefab) ?? string.Empty : string.Empty;

        private static bool HasOtherPrimary(DynamicBuffer<VehicleModel> models, DynamicBuffer<BTS_ConsistCar> cars) {
            for (var i = 0; i < models.Length; i++) {
                var primary = models[i].m_PrimaryPrefab;
                if (primary == Entity.Null) {
                    continue;
                }

                var inTrain = false;
                for (var j = 0; j < cars.Length && !inTrain; j++) {
                    inTrain = cars[j].m_Prefab == primary;
                }

                if (!inTrain) {
                    return true;
                }
            }

            return false;
        }

        private float LengthOf(Entity prefab) =>
            EntityManager.TryGetComponent(prefab, out ObjectGeometryData geometry) ? geometry.m_Size.z : 0f;

        /// <summary>The car's picture. Callers pass a custom car's original: the copy has no asset to find one on.</summary>
        private string ThumbnailOf(Entity prefab) =>
            prefab != Entity.Null && EntityManager.Exists(prefab)
                ? m_ImageSystem.GetThumbnail(prefab) ?? m_ImageSystem.placeholderIcon
                : m_ImageSystem.placeholderIcon;

        /// <summary>A car prefab's figures as the simulation reads them; zeros until the prefab has initialised.</summary>
        private CarFigures FiguresOf(Entity prefab) {
            var em = EntityManager;
            em.TryGetComponent(prefab, out PublicTransportVehicleData passengers);
            em.TryGetComponent(prefab, out CargoTransportVehicleData cargo);
            em.TryGetComponent(prefab, out TrainData train);
            return new CarFigures(passengers.m_PassengerCapacity, cargo.m_CargoCapacity, train);
        }

        private static bool ValidBlock(List<BTS_ConsistCar> cars, int start, int count) =>
            start >= 0 && count > 0 && start + count <= cars.Count;

        #endregion
    }
}
