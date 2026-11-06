namespace BetterTransitSelector.Systems {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal.Entities;

    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Pathfind;
    using Game.Prefabs;
    using Game.Routes;
    using Game.Vehicles;

    using BetterTransitSelector.Components;
    using BetterTransitSelector.Domain;

    using ModsCommon.Systems;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// Gives each train a line sends out the line's player-built consist (<see cref="BTS_ConsistCar"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A depot puts a train on a line in one of two ways, and both are handled here:
    /// </para>
    /// <list type="bullet">
    /// <item><b>New train.</b> <c>TransportVehicleSelectData.CreateVehicle</c> builds a controller plus one entity
    /// per car, listed in the controller's <see cref="LayoutElement"/> buffer and tagged <see cref="Created"/>.
    /// It runs inside Burst jobs no patch reaches, so the train is rewritten after its command buffer plays back
    /// and before <c>Game.Vehicles.InitializeSystem</c>, which then places the cars as if vanilla built them.</item>
    /// <item><b>Parked train reused.</b> When the pathfinder picks a train parked in the depot,
    /// <c>TransportDepotAISystem</c> converts it from parked to moving in place: no <see cref="Created"/>, so
    /// InitializeSystem never re-places it. It is detected here as a controller that was parked on the previous
    /// update and now has a route, rewritten, and placed along its new path with the same vanilla helpers
    /// InitializeSystem uses (<c>PathUtils.InitializeSpawnPath</c>, <c>VehicleUtils.UpdateCarriageLocations</c>).
    /// This is the common path: depots keep a stock of parked trains and prefer them.</item>
    /// </list>
    /// <para>
    /// Capacity needs no fix-up: boarding, cargo loading and every capacity readout sum the prefab data of the
    /// cars actually in the layout.
    /// </para>
    /// <para>
    /// Every car is built from scratch from its prefab's archetype; vanilla's cars are all removed. The one
    /// entity kept is vanilla's controller, which becomes the built train's lead car: the depot has already
    /// pointed the route's vehicle list, the dispatch request and the line at it.
    /// </para>
    /// </remarks>
    public partial class BTS_ConsistSystem : CommonGameSystemBase {
        private EntityQuery m_NewTrainQuery;
        private EntityQuery m_DispatchedQuery;
        private EntityQuery m_ParkedQuery;

        /// <summary>Train controllers parked in a depot as of the previous update.</summary>
        private NativeParallelHashSet<Entity> m_WasParked;

        /// <summary>Lines already warned about, so a misconfigured line logs once, not per train.</summary>
        private readonly HashSet<Entity> m_Warned = new HashSet<Entity>();

        protected override void OnCreate() {
            base.OnCreate();

            // Controllers only: cars have Train but neither LayoutElement nor CurrentRoute.
            m_NewTrainQuery = SystemAPI.QueryBuilder()
                                       .WithAll<Created, Unspawned, Train, CurrentRoute, PrefabRef>()
                                       .WithAllRW<LayoutElement>()
                                       .Build();

            // Parked-to-moving conversion adds Updated, TrainCurrentLane and PathOwner and removes ParkedTrain.
            // Updated alone also fires on trains already running, which is why a match here only counts when
            // the same controller was parked on the previous update.
            m_DispatchedQuery = SystemAPI.QueryBuilder()
                                         .WithAll<Updated, Train, CurrentRoute, PrefabRef, TrainCurrentLane, PathOwner>()
                                         .WithAllRW<LayoutElement>()
                                         .WithNone<Created, ParkedTrain, Deleted>()
                                         .Build();

            m_ParkedQuery = SystemAPI.QueryBuilder()
                                     .WithAll<ParkedTrain, LayoutElement>()
                                     .WithNone<Deleted>()
                                     .Build();

            m_WasParked = new NativeParallelHashSet<Entity>(64, Allocator.Persistent);
        }

        protected override void OnDestroy() {
            m_WasParked.Dispose();
            base.OnDestroy();
        }

        protected override void OnUpdate() {
            if (!m_NewTrainQuery.IsEmpty) {
                using var trains = m_NewTrainQuery.ToEntityArray(Allocator.Temp);
                foreach (var controller in trains) {
                    TryRebuild(controller, reused: false);
                }
            }

            if (!m_DispatchedQuery.IsEmpty && !m_WasParked.IsEmpty) {
                using var trains = m_DispatchedQuery.ToEntityArray(Allocator.Temp);
                foreach (var controller in trains) {
                    if (m_WasParked.Contains(controller)) {
                        TryRebuild(controller, reused: true);
                    }
                }
            }

            m_WasParked.Clear();
            using (var parked = m_ParkedQuery.ToEntityArray(Allocator.Temp)) {
                foreach (var controller in parked) {
                    m_WasParked.Add(controller);
                }
            }
        }

        private void TryRebuild(Entity controller, bool reused) {
            var route = EntityManager.GetComponentData<CurrentRoute>(controller).m_Route;
            if (route == Entity.Null
                || !EntityManager.Exists(route)
                || !EntityManager.TryGetBuffer(route, true, out DynamicBuffer<BTS_ConsistCar> build)
                || build.Length == 0) {
                return;
            }

            // Copy out: the structural changes below invalidate the buffer.
            using var cars = build.ToNativeArray(Allocator.Temp);
            if (Rebuild(controller, route, cars, reused) && reused) {
                Place(controller);
            }
        }

        /// <summary>Replaces the train's cars with <paramref name="cars"/>. False when the train was left as it was.</summary>
        private bool Rebuild(Entity controller, Entity route, NativeArray<BTS_ConsistCar> cars, bool reused) {
            var em               = EntityManager;
            var controllerPrefab = em.GetComponentData<PrefabRef>(controller).m_Prefab;

            if (ConsistRules.Matches(em, controller, cars)) {
                // A train that already has this consist, typically one of ours returning to the depot.
                return false;
            }

            // A train already in the world keeps its controller's prefab: only a Created entity goes through
            // InitializeSystem, which is what makes swapping the prefab under it safe.
            var lead = reused ? FindOwnPrefab(cars, controllerPrefab) : FindLead(cars, controllerPrefab, route);
            if (lead < 0) {
                WarnOnce(route, reused
                    ? $"the depot reused a parked '{PrefabName(controllerPrefab)}', which the built train does not contain; kept it as it was."
                    : $"no car in the built train can lead a '{PrefabName(controllerPrefab)}' train; kept vanilla.");
                return false;
            }

            // Every car but the controller goes; the controller stays because the depot has already pointed the
            // route's vehicle list, the dispatch request and the line at that exact entity.
            using (var original = em.GetBuffer<LayoutElement>(controller, true).ToNativeArray(Allocator.Temp)) {
                for (var i = 0; i < original.Length; i++) {
                    var car = original[i].m_Vehicle;
                    if (car == controller) {
                        continue;
                    }

                    // A new car can simply go; one already in the world is deleted the way vanilla does it
                    // (VehicleUtils.DeleteVehicle), so its cleanup runs.
                    if (reused) {
                        em.AddComponent<Deleted>(car);
                    } else {
                        em.DestroyEntity(car);
                    }
                }
            }

            // Then the train is built from scratch, front to back, from each car's own prefab.
            using var layout = new NativeList<Entity>(cars.Length, Allocator.Temp);
            var random = Random.CreateFromIndex((uint)controller.Index);
            for (var i = 0; i < cars.Length; i++) {
                var prefab = cars[i].m_Prefab;
                Entity car;
                if (i == lead) {
                    car = controller;
                    if (prefab != controllerPrefab) {
                        em.SetComponentData(controller, new PrefabRef(prefab));
                    }
                } else if (!IsUsable(prefab)) {
                    // A car whose asset is no longer installed: drop the slot, keep the rest of the train.
                    continue;
                } else {
                    car = CreateCar(prefab, controller, ref random);
                }

                SetReversed(car, cars[i].m_Reversed);
                layout.Add(car);
            }

            var buffer = em.GetBuffer<LayoutElement>(controller);
            buffer.Clear();
            for (var i = 0; i < layout.Length; i++) {
                buffer.Add(new LayoutElement(layout[i]));
            }

            return true;
        }

        /// <summary>
        /// Lays a rebuilt, reused train along the start of its path, the way InitializeSystem lays a new one:
        /// centred on the path's first lane, cars placed front to back, bogie frames reset.
        /// </summary>
        private void Place(Entity controller) {
            var em = EntityManager;
            if (!em.TryGetBuffer(controller, false, out DynamicBuffer<PathElement> path) || path.Length == 0) {
                return;
            }

            var prefabRefs   = SystemAPI.GetComponentLookup<PrefabRef>(true);
            var trainPrefabs = SystemAPI.GetComponentLookup<TrainData>(true);
            var layout       = em.GetBuffer<LayoutElement>(controller);
            var pathOwner    = em.GetComponentData<PathOwner>(controller);
            var length       = VehicleUtils.CalculateLength(controller, layout, ref prefabRefs, ref trainPrefabs);

            var curves          = SystemAPI.GetComponentLookup<Curve>(true);
            var lanes           = SystemAPI.GetComponentLookup<Lane>(true);
            var edgeLanes       = SystemAPI.GetComponentLookup<EdgeLane>(true);
            var owners          = SystemAPI.GetComponentLookup<Owner>(true);
            var edges           = SystemAPI.GetComponentLookup<Game.Net.Edge>(true);
            var spawnLocations  = SystemAPI.GetComponentLookup<Game.Objects.SpawnLocation>(true);
            var connectedEdges  = SystemAPI.GetBufferLookup<ConnectedEdge>(true);
            var subLanes        = SystemAPI.GetBufferLookup<Game.Net.SubLane>(true);
            var connectionLanes = SystemAPI.GetComponentLookup<Game.Net.ConnectionLane>(true);

            var trains       = SystemAPI.GetComponentLookup<Train>(false);
            var parkedTrains = SystemAPI.GetComponentLookup<ParkedTrain>(false);
            var currentLanes = SystemAPI.GetComponentLookup<TrainCurrentLane>(false);
            var navigations  = SystemAPI.GetComponentLookup<TrainNavigation>(false);
            var transforms   = SystemAPI.GetComponentLookup<Transform>(false);

            using var laneBuffer = new NativeList<PathElement>(10, Allocator.Temp);
            PathUtils.InitializeSpawnPath(path, laneBuffer, Entity.Null, ref pathOwner, length,
                ref curves, ref lanes, ref edgeLanes, ref owners, ref edges, ref spawnLocations, ref connectedEdges, ref subLanes);
            VehicleUtils.UpdateCarriageLocations(layout, laneBuffer,
                ref trains, ref parkedTrains, ref currentLanes, ref navigations, ref transforms, ref curves,
                ref connectionLanes, ref prefabRefs, ref trainPrefabs);
            em.SetComponentData(controller, pathOwner);

            // InitializeSystem.UpdateBogieFrames, which only runs for Created trains.
            for (var i = 0; i < layout.Length; i++) {
                var car = layout[i].m_Vehicle;
                if (!currentLanes.TryGetComponent(car, out var currentLane)
                    || !em.TryGetBuffer(car, false, out DynamicBuffer<TrainBogieFrame> frames)) {
                    continue;
                }

                frames.ResizeUninitialized(4);
                for (var j = 0; j < frames.Length; j++) {
                    frames[j] = new TrainBogieFrame {
                        m_FrontLane = currentLane.m_Front.m_Lane,
                        m_RearLane  = currentLane.m_Rear.m_Lane,
                    };
                }
            }
        }

        private static int FindOwnPrefab(NativeArray<BTS_ConsistCar> cars, Entity controllerPrefab) {
            for (var i = 0; i < cars.Length; i++) {
                if (cars[i].m_Prefab == controllerPrefab) {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Picks the car slot the existing controller entity becomes: the first slot of the controller's own
        /// prefab, else the first primary-capable car the line has selected whose controller archetype matches,
        /// so swapping the controller's prefab keeps its component set valid. -1 when neither exists.
        /// </summary>
        private int FindLead(NativeArray<BTS_ConsistCar> cars, Entity controllerPrefab, Entity route) {
            var own = FindOwnPrefab(cars, controllerPrefab);
            if (own >= 0) {
                return own;
            }

            var em = EntityManager;
            if (!em.TryGetComponent(controllerPrefab, out TrainObjectData ownData)
                || !em.TryGetBuffer(route, true, out DynamicBuffer<VehicleModel> models)) {
                return -1;
            }

            for (var i = 0; i < cars.Length; i++) {
                var prefab = cars[i].m_Prefab;
                if (IsUsable(prefab)
                    && ConsistRules.IsPrimary(em, prefab)
                    && ConsistRules.PrimaryAccepted(models, prefab)
                    && em.TryGetComponent(prefab, out TrainObjectData other)
                    && other.m_ControllerArchetype == ownData.m_ControllerArchetype) {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Builds one car from its prefab's archetype, the way <c>CreateVehicle</c> does.</summary>
        private Entity CreateCar(Entity prefab, Entity controller, ref Random random) {
            var em  = EntityManager;
            var car = em.CreateEntity(em.GetComponentData<ObjectData>(prefab).m_Archetype);

            // The archetype normally carries these already. Stated anyway: a car added to a train already in the
            // world is only picked up by rendering, the search trees and InitializeSystem if it is Created.
            em.AddComponent<Created>(car);
            em.AddComponent<Updated>(car);

            em.SetComponentData(car, em.GetComponentData<Transform>(controller));
            em.SetComponentData(car, new PrefabRef(prefab));
            em.SetComponentData(car, new Controller(controller));
            em.SetComponentData(car, new Train(0));
            em.SetComponentData(car, new PseudoRandomSeed(ref random));

            if (em.TryGetComponent(controller, out TripSource tripSource)) {
                em.AddComponentData(car, tripSource);
            }

            if (em.HasComponent<Unspawned>(controller)) {
                em.AddComponent<Unspawned>(car);
            }

            CopyTag<PassengerTransport>(controller, car);
            CopyTag<EvacuatingTransport>(controller, car);
            CopyTag<PrisonerTransport>(controller, car);
            return car;
        }

        private void CopyTag<T>(Entity from, Entity to) where T : unmanaged, IComponentData {
            if (EntityManager.HasComponent<T>(from)) {
                EntityManager.AddComponent<T>(to);
            }
        }

        private void SetReversed(Entity car, bool reversed) {
            var train = EntityManager.GetComponentData<Train>(car);
            train.m_Flags = reversed
                ? train.m_Flags | Game.Vehicles.TrainFlags.Reversed
                : train.m_Flags & ~Game.Vehicles.TrainFlags.Reversed;
            EntityManager.SetComponentData(car, train);
        }

        /// <inheritdoc cref="ConsistRules.IsUsable"/>
        private bool IsUsable(Entity prefab) => ConsistRules.IsUsable(EntityManager, prefab);

        private void WarnOnce(Entity route, string message) {
            if (m_Warned.Add(route)) {
                m_Log.Warn($"Rebuild() -- line {route.Index}:{route.Version}: {message}");
            }
        }

        private string PrefabName(Entity prefab) =>
            World.GetExistingSystemManaged<PrefabSystem>()?.GetPrefabName(prefab) ?? prefab.ToString();
    }
}
