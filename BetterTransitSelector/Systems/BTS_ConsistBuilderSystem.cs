namespace BetterTransitSelector.Systems {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Colossal.Entities;

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
    /// Backs the train builder in the vehicle section: publishes the selected line's built train and the
    /// cars that can run on it, and edits the line's <see cref="BTS_ConsistCar"/> buffer.
    /// </summary>
    /// <remarks>
    /// Model selection is never written here. When the built train needs a model the line has not
    /// selected, the UI offers a fix that goes through vanilla's own <c>selectVehicles</c> trigger.
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
            var signature = Signature(line);
            if (signature != m_Signature) {
                m_Signature      = signature;
                m_Builder.Value = line == Entity.Null ? ConsistBuilder.Empty : Build(line, track, lineData);
            }

            base.OnUpdate();
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
            em.TryGetBuffer(line, true, out DynamicBuffer<BTS_ConsistCar> cars);
            FillRelated(builder.Related, models, cars);

            builder.Refitting = em.HasComponent<BTS_Refit>(line);
            if (cars.IsCreated && cars.Length > 0) {
                using var build = cars.ToNativeArray(Allocator.Temp);
                builder.Mismatched = ConsistRules.CountMismatched(em, line, build);
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

            if (!cars.IsCreated || cars.Length == 0) {
                return builder;
            }

            var lead         = -1;
            var firstPrimary = Entity.Null;
            var firstEngine  = Entity.Null;
            var engineOk     = false;
            for (var i = 0; i < cars.Length; i++) {
                var prefab = cars[i].m_Prefab;
                if (!IsCatalogueCar(prefab)) {
                    continue;
                }

                if (ConsistRules.IsPrimary(em, prefab)) {
                    if (firstPrimary == Entity.Null) {
                        firstPrimary = prefab;
                    }

                    if (lead < 0 && (!models.IsCreated || ConsistRules.PrimaryAccepted(models, prefab))) {
                        lead = i;
                    }
                }

                if (ConsistRules.IsSecondary(em, prefab)) {
                    if (firstEngine == Entity.Null) {
                        firstEngine = prefab;
                    }

                    engineOk |= models.IsCreated && ConsistRules.SecondaryAccepted(models, prefab);
                }
            }

            for (var i = 0; i < cars.Length; i++) {
                var prefab = cars[i].m_Prefab;
                // A custom car reports its asset too, so the UI can name it and show which figures changed.
                var custom  = m_CustomCars.TryDescribe(prefab, out var original, out _);
                var figures = FiguresOf(prefab);
                builder.Cars.Add(new ConsistSlot(
                    prefab, NameOf(prefab), cars[i].m_Reversed, !IsCatalogueCar(prefab), i == lead,
                    custom ? NameOf(original) : string.Empty,
                    KindOf(prefab),
                    LengthOf(prefab),
                    ThumbnailOf(custom ? original : prefab),
                    figures,
                    custom ? FiguresOf(original) : figures));
            }

            if (lead < 0) {
                builder.Status     = firstPrimary == Entity.Null ? "NoLead" : "NeedsPrimary";
                builder.FixPrimary = firstPrimary;
            } else if (models.IsCreated
                       && ConsistRules.HasSecondary(models)
                       && !em.HasComponent<MultipleUnitTrainData>(cars[lead].m_Prefab)
                       && !engineOk) {
                // Vanilla retires a locomotive-hauled train that carries none of the line's selected
                // locomotives (RouteUtils.CheckVehicleModel); multiple-unit trains are exempt from that check.
                builder.Status       = firstEngine == Entity.Null ? "NoEngine" : "NeedsSecondary";
                builder.FixSecondary = firstEngine;
            } else if (models.IsCreated && HasOtherPrimary(models, cars)) {
                // A parked train of another selected lead model can only be rebuilt if its lead car's prefab is
                // in the built train (BTS_ConsistSystem never swaps the prefab of a train already in the world).
                builder.Status = "OtherModels";
            } else {
                builder.Status = "Ok";
            }

            return builder;
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
        private void FillRelated(List<Entity> related, DynamicBuffer<VehicleModel> models, DynamicBuffer<BTS_ConsistCar> cars) {
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

            if (cars.IsCreated) {
                for (var i = 0; i < cars.Length; i++) {
                    Add(cars[i].m_Prefab);
                }
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
                if (em.TryGetBuffer(line, true, out DynamicBuffer<BTS_ConsistCar> cars)) {
                    hash = hash * 31 + cars.Length + 1;
                    for (var i = 0; i < cars.Length; i++) {
                        hash = hash * 31 + cars[i].m_Prefab.Index * 2 + (cars[i].m_Reversed ? 1 : 0);
                        // A custom car's data fills in the frame after it is created; its speed moving from 0
                        // republishes the slot with real figures.
                        if (em.TryGetComponent(cars[i].m_Prefab, out TrainData train)) {
                            hash = hash * 31 + train.m_MaxSpeed.GetHashCode();
                        }
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

        /// <summary>
        /// Applies <paramref name="change"/> to the selected line's built train. An empty result removes the
        /// buffer, so a line the player has reset carries nothing of this mod in the save.
        /// </summary>
        private void Edit(System.Action<List<BTS_ConsistCar>> change) {
            var line = SelectedRailLine(out _, out _);
            if (line == Entity.Null) {
                return;
            }

            var em   = EntityManager;
            var cars = em.TryGetBuffer(line, true, out DynamicBuffer<BTS_ConsistCar> existing)
                ? existing.AsNativeArray().ToList()
                : new List<BTS_ConsistCar>();

            change(cars);

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
            m_ImageSystem.GetThumbnail(prefab) ?? m_ImageSystem.placeholderIcon;

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
