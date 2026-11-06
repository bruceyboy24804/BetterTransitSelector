namespace BetterTransitSelector.Systems {
    #region Using Statements

    using Colossal.Entities;

    using Game.Economy;
    using Game.Prefabs;
    using Game.Routes;
    using Game.Vehicles;

    using BetterTransitSelector.Components;

    using ModsCommon.Systems;

    using Unity.Collections;
    using Unity.Entities;

    #endregion

    /// <summary>
    /// Stops vanilla swapping a cargo train's cars at a stop when the swap would take the train out of service,
    /// and always on a line with a player-built train.
    /// </summary>
    /// <remarks>
    /// <para>
    /// How vanilla loads a cargo train: <c>TransportBoardingHelpers.BeginBoarding</c> loads what the train's cars
    /// can already take straight into them, and writes what did not fit into the train's
    /// <see cref="LoadingResources"/> buffer. On a later update, <c>TransportTrainAISystem.StopBoarding</c> reads
    /// that buffer and swaps each still-empty cargo car for one <c>TransportTrainCarriageSelectData</c> picks for
    /// the resource, from every cargo car in the game rather than from the line's selection. After that,
    /// <c>RouteUtils.CheckVehicleModel</c> can find no car the line selected, and <c>TransportLineSystem</c>
    /// sends the train back to its depot.
    /// </para>
    /// <para>
    /// The buffer is only the swap plan: nothing else reads it. Clearing it between the two updates means no
    /// swap. The train leaves with what its own cars took, and the rest waits at the station for the next train.
    /// </para>
    /// <para>
    /// A line without a built train keeps vanilla's swap unless it would leave the train with no car the line
    /// accepts. The line's own selection may be a car that carries nothing until it is swapped.
    /// </para>
    /// </remarks>
    public partial class BTS_CargoCarSystem : CommonGameSystemBase {
        private EntityQuery m_CargoTrainQuery;

        protected override void OnCreate() {
            base.OnCreate();

            // Controllers only: the cars carry neither the loading plan nor the route.
            m_CargoTrainQuery = SystemAPI.QueryBuilder()
                                         .WithAll<Train, CurrentRoute, LayoutElement>()
                                         .WithAllRW<LoadingResources>()
                                         .Build();
            RequireForUpdate(m_CargoTrainQuery);
        }

        protected override void OnUpdate() {
            using var trains = m_CargoTrainQuery.ToEntityArray(Allocator.Temp);
            foreach (var train in trains) {
                var plan = EntityManager.GetBuffer<LoadingResources>(train, true);
                if (plan.Length != 0 && ShouldKeepCars(train)) {
                    EntityManager.GetBuffer<LoadingResources>(train).Clear();
                }
            }
        }

        private bool ShouldKeepCars(Entity train) {
            var em    = EntityManager;
            var route = em.GetComponentData<CurrentRoute>(train).m_Route;
            if (route == Entity.Null || !em.Exists(route)) {
                return false;
            }

            if (em.TryGetBuffer(route, true, out DynamicBuffer<BTS_ConsistCar> build) && build.Length != 0) {
                return true;
            }

            return em.TryGetBuffer(route, true, out DynamicBuffer<VehicleModel> models)
                   && ChecksSecondary(models, train)
                   && !KeepsAcceptedCar(train, models);
        }

        /// <summary>
        /// True when a car the line accepts would survive the swap: the swap only touches cargo cars with nothing
        /// loaded, so an accepted car that already holds cargo, or is not a cargo car, stays as it is.
        /// </summary>
        private bool KeepsAcceptedCar(Entity train, DynamicBuffer<VehicleModel> models) {
            var em = EntityManager;
            foreach (var element in em.GetBuffer<LayoutElement>(train, true)) {
                var car = element.m_Vehicle;
                if (!em.TryGetComponent(car, out PrefabRef prefabRef) || !IsSecondary(models, prefabRef.m_Prefab)) {
                    continue;
                }

                var swappable = em.HasComponent<CargoTransportVehicleData>(prefabRef.m_Prefab)
                                && em.TryGetBuffer(car, true, out DynamicBuffer<Resources> resources)
                                && resources.Length == 0;
                if (!swappable) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Mirror of <c>RouteUtils.CheckVehicleModel</c>: cars are checked only when the line selected a car
        /// model and the train is not a multiple unit.</summary>
        private bool ChecksSecondary(DynamicBuffer<VehicleModel> models, Entity train) {
            var any = false;
            foreach (var model in models) {
                any |= model.m_SecondaryPrefab != Entity.Null;
            }

            return any
                   && EntityManager.TryGetComponent(train, out PrefabRef prefabRef)
                   && !EntityManager.HasComponent<MultipleUnitTrainData>(prefabRef.m_Prefab);
        }

        private static bool IsSecondary(DynamicBuffer<VehicleModel> models, Entity prefab) {
            foreach (var model in models) {
                if (model.m_SecondaryPrefab == prefab) {
                    return true;
                }
            }

            return false;
        }
    }
}
