namespace BetterTransitSelector.Domain {
    #region Using Statements

    using Colossal.Entities;

    using Game.Net;
    using Game.Prefabs;
    using Game.Routes;
    using Game.Vehicles;

    using BetterTransitSelector.Components;

    using Unity.Collections;
    using Unity.Entities;

    #endregion

    /// <summary>
    /// How vanilla classifies train cars, restated in one place so the builder UI and the spawn-time
    /// rewrite agree on it.
    /// </summary>
    /// <remarks>
    /// The split mirrors <c>TransportVehicleSelectData.GetRandomVehicle</c>: a line's <b>primary</b> model is a
    /// multiple-unit front (<see cref="MultipleUnitTrainData"/> + <see cref="TrainEngineData"/>) or a plain
    /// carriage (<see cref="TrainCarriageData"/> without <see cref="MultipleUnitTrainData"/>), and it becomes the
    /// train's controller entity. The <b>secondary</b> model is a locomotive (<see cref="TrainEngineData"/>
    /// without <see cref="MultipleUnitTrainData"/>). Anything else (tenders, multiple-unit middle cars) is
    /// neither, and can only ride along.
    /// </remarks>
    internal static class ConsistRules {
        /// <summary>Car can be a line's primary model, and so the train's controller.</summary>
        public static bool IsPrimary(EntityManager em, Entity prefab) =>
            em.HasComponent<TrainObjectData>(prefab)
            && (em.HasComponent<MultipleUnitTrainData>(prefab)
                ? em.HasComponent<TrainEngineData>(prefab)
                : em.HasComponent<TrainCarriageData>(prefab));

        /// <summary>Car can be a line's secondary model (a locomotive).</summary>
        public static bool IsSecondary(EntityManager em, Entity prefab) =>
            em.HasComponent<TrainEngineData>(prefab) && !em.HasComponent<MultipleUnitTrainData>(prefab);

        /// <summary>
        /// Whether the line's selection lets a train containing <paramref name="prefab"/> as its controller
        /// survive vanilla's <c>RouteUtils.CheckVehicleModel</c>, which retires trains whose models are no longer
        /// selected.
        /// </summary>
        public static bool PrimaryAccepted(DynamicBuffer<VehicleModel> models, Entity prefab) {
            var any = false;
            for (var i = 0; i < models.Length; i++) {
                if (models[i].m_PrimaryPrefab == Entity.Null) {
                    continue;
                }

                any = true;
                if (models[i].m_PrimaryPrefab == prefab) {
                    return true;
                }
            }

            return !any;
        }

        /// <summary>True when the line has at least one secondary (locomotive) model selected.</summary>
        public static bool HasSecondary(DynamicBuffer<VehicleModel> models) {
            for (var i = 0; i < models.Length; i++) {
                if (models[i].m_SecondaryPrefab != Entity.Null) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when <paramref name="prefab"/> is one of the line's selected secondary models.</summary>
        public static bool SecondaryAccepted(DynamicBuffer<VehicleModel> models, Entity prefab) {
            for (var i = 0; i < models.Length; i++) {
                if (models[i].m_SecondaryPrefab == prefab) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A car prefab that can still be spawned. Guards saves whose built train names an asset since removed:
        /// the saved entity then resolves to nothing, or to a prefab whose <see cref="PrefabData"/> is disabled.
        /// </summary>
        public static bool IsUsable(EntityManager em, Entity prefab) =>
            prefab != Entity.Null
            && em.Exists(prefab)
            && em.HasComponent<PrefabData>(prefab)
            && em.IsComponentEnabled<PrefabData>(prefab)
            && em.HasComponent<TrainData>(prefab)
            && em.HasComponent<ObjectData>(prefab);

        /// <summary>
        /// True when the train <paramref name="controller"/> leads already runs exactly the built train
        /// <paramref name="cars"/>: same cars, same order, same directions. Slots whose asset is gone are skipped,
        /// as the rebuild skips them.
        /// </summary>
        public static bool Matches(EntityManager em, Entity controller, NativeArray<BTS_ConsistCar> cars) {
            if (!em.TryGetBuffer(controller, true, out DynamicBuffer<LayoutElement> layout)) {
                return false;
            }

            var j = 0;
            for (var i = 0; i < cars.Length; i++) {
                if (!IsUsable(em, cars[i].m_Prefab)) {
                    continue;
                }

                if (j >= layout.Length) {
                    return false;
                }

                var car = layout[j++].m_Vehicle;
                if (!em.TryGetComponent(car, out PrefabRef prefabRef) || prefabRef.m_Prefab != cars[i].m_Prefab
                    || !em.TryGetComponent(car, out Train train)
                    || ((train.m_Flags & Game.Vehicles.TrainFlags.Reversed) != 0) != cars[i].m_Reversed) {
                    return false;
                }
            }

            return j == layout.Length;
        }

        /// <summary>
        /// The line's running trains that differ from the built train and are not already leaving it (vanilla's
        /// <see cref="PublicTransportFlags.AbandonRoute"/>): what a rolling refit still has to replace.
        /// </summary>
        public static int CountMismatched(EntityManager em, Entity line, NativeArray<BTS_ConsistCar> cars) {
            var count = 0;
            ForEachMismatch(em, line, cars, (train, distance) => count++);
            return count;
        }

        /// <summary>
        /// The mismatched train that has travelled furthest, the one vanilla itself retires first when a line has
        /// too many (<c>TransportLineSystem.AbandonVehicles</c> sorts by odometer). Null when none is left.
        /// </summary>
        public static Entity MostWornMismatch(EntityManager em, Entity line, NativeArray<BTS_ConsistCar> cars) {
            var best     = Entity.Null;
            var furthest = -1f;
            ForEachMismatch(em, line, cars, (train, distance) => {
                if (distance > furthest) {
                    furthest = distance;
                    best     = train;
                }
            });
            return best;
        }

        private static void ForEachMismatch(EntityManager em, Entity line, NativeArray<BTS_ConsistCar> cars,
                                            System.Action<Entity, float> visit) {
            if (!em.TryGetBuffer(line, true, out DynamicBuffer<RouteVehicle> vehicles)) {
                return;
            }

            for (var i = 0; i < vehicles.Length; i++) {
                var train = vehicles[i].m_Vehicle;
                if (!em.Exists(train)
                    || !em.TryGetComponent(train, out CurrentRoute route) || route.m_Route != line
                    || (em.TryGetComponent(train, out Game.Vehicles.PublicTransport transport)
                        && (transport.m_State & PublicTransportFlags.AbandonRoute) != 0)
                    || Matches(em, train, cars)) {
                    continue;
                }

                visit(train, em.TryGetComponent(train, out Odometer odometer) ? odometer.m_Distance : 0f);
            }
        }

        /// <summary>The track a line's trains run on, or <see cref="TrackTypes.None"/> for non-rail lines.</summary>
        public static TrackTypes TrackOf(TransportType type) =>
            type switch {
                TransportType.Train  => TrackTypes.Train,
                TransportType.Tram   => TrackTypes.Tram,
                TransportType.Subway => TrackTypes.Subway,
                _                    => TrackTypes.None,
            };
    }
}
