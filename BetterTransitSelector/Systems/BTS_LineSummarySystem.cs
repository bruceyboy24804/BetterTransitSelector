namespace BetterTransitSelector.Systems {
    #region Using Statements

    using BetterTransitSelector.Domain;

    using Game.Net;
    using Game.Pathfind;
    using Game.Prefabs;
    using Game.Routes;
    using Game.Simulation;
    using Game.UI.InGame;

    using ModsCommon.Systems;

    using Unity.Entities;

    using UnityEngine;

    #endregion

    /// <summary>
    /// Publishes the selected transport line's fleet figures for the picker's summary row.
    /// </summary>
    /// <remarks>
    /// A getter binding re-read each UI frame: the selection and the line's vehicles change at
    /// any moment, and the read is a handful of component lookups on one entity, so there is
    /// nothing worth caching. Runs on the main thread because the selected entity is only
    /// available there; vanilla's own section schedules a job for the same reads, which for one
    /// entity is more machinery than the work.
    /// </remarks>
    public partial class BTS_LineSummarySystem : CommonUISystemBase {
        /// <inheritdoc/>
        protected override string ModId => Mod.Instance.Id;

        private SelectedInfoUISystem m_SelectedInfo;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_SelectedInfo = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            CreateBinding("lineSummary", Read);
        }

        private LineSummary Read() {
            var line = m_SelectedInfo.selectedEntity;
            var em   = EntityManager;
            if (line == Entity.Null || !em.Exists(line)
                || !em.HasComponent<TransportLine>(line)
                || !em.HasBuffer<RouteVehicle>(line)
                || !em.HasBuffer<RouteWaypoint>(line)
                || !em.HasBuffer<RouteSegment>(line)
                || !em.HasComponent<PrefabRef>(line)) {
                return new LineSummary(false, 0, 0, 0f, 0f, 0, 0);
            }

            var prefab = em.GetComponentData<PrefabRef>(line).m_Prefab;
            if (!em.HasComponent<TransportLineData>(prefab)) {
                return new LineSummary(false, 0, 0, 0f, 0f, 0, 0);
            }

            var lineData = em.GetComponentData<TransportLineData>(prefab);
            var interval = lineData.m_DefaultVehicleInterval;
            if (em.HasBuffer<RouteModifier>(line)) {
                RouteUtils.ApplyModifier(ref interval, em.GetBuffer<RouteModifier>(line, true), RouteModifierType.VehicleInterval);
            }

            var duration = StableDuration(line, lineData);
            var target   = TransportLineSystem.CalculateVehicleCount(interval, duration);

            // The game's own demand reading: riders and seats summed over the live vehicles, the
            // same call the transport overview's "usage" comes from.
            var riders   = 0;
            var capacity = 0;
            var count    = TransportUIUtils.GetRouteVehiclesCount(em, line, ref riders, ref capacity);

            return new LineSummary(true, count, target, interval, duration, riders, capacity);
        }

        /// <summary>
        /// One loop's duration, stops included -- vanilla's <c>CalculateStableDuration</c>, starting
        /// from the first timed waypoint so every segment is counted once.
        /// </summary>
        private float StableDuration(Entity line, TransportLineData lineData) {
            var em        = EntityManager;
            var waypoints = em.GetBuffer<RouteWaypoint>(line, true);
            var segments  = em.GetBuffer<RouteSegment>(line, true);
            if (waypoints.Length == 0 || segments.Length == 0) {
                return 0f;
            }

            var start = 0;
            for (var i = 0; i < waypoints.Length; i++) {
                if (em.HasComponent<VehicleTiming>(waypoints[i].m_Waypoint)) {
                    start = i;
                    break;
                }
            }

            var total = 0f;
            for (var j = 0; j < waypoints.Length; j++) {
                var segIndex = (start + j) % waypoints.Length;
                var wpIndex  = (start + j + 1) % waypoints.Length;
                if (segIndex < segments.Length) {
                    var segment = segments[segIndex].m_Segment;
                    if (em.HasComponent<PathInformation>(segment)) {
                        total += em.GetComponentData<PathInformation>(segment).m_Duration;
                    }
                }
                if (em.HasComponent<VehicleTiming>(waypoints[wpIndex].m_Waypoint)) {
                    total += lineData.m_StopDuration;
                }
            }

            return total;
        }
    }
}
