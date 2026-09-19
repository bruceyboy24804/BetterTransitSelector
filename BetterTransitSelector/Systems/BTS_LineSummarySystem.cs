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
                return new LineSummary(false, 0, 0, 0f, 0f, 0, 0, 0);
            }

            var prefab = em.GetComponentData<PrefabRef>(line).m_Prefab;
            if (!em.HasComponent<TransportLineData>(prefab)) {
                return new LineSummary(false, 0, 0, 0f, 0f, 0, 0, 0);
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

            // 0 switches the whole platform-fit feature off in the UI: the marker, the tooltip
            // line and the Fits filter all key off a positive length.
            var platform = IsRail(lineData.m_TransportType) && ((Setting)Mod.Instance.Settings).PlatformFit
                ? ShortestPlatform(line)
                : 0;

            return new LineSummary(true, count, target, interval, duration, riders, capacity, platform);
        }

        private static bool IsRail(TransportType type) =>
            type == TransportType.Train || type == TransportType.Subway;

        /// <summary>
        /// The shortest lane the line's vehicles stop on, in whole metres; 0 when none is found.
        /// </summary>
        /// <remarks>
        /// Each waypoint that is a stop carries a <c>RouteLane</c> naming the lane the vehicle
        /// halts on -- at a station, the platform track -- and that lane's <c>Curve</c> knows its
        /// length. The end lane is the one the vehicle is on when it stops; the start lane is the
        /// fallback for waypoints that only carry one.
        /// </remarks>
        private int ShortestPlatform(Entity line) {
            var em        = EntityManager;
            var waypoints = em.GetBuffer<RouteWaypoint>(line, true);
            var shortest  = float.MaxValue;

            for (var i = 0; i < waypoints.Length; i++) {
                var waypoint = waypoints[i].m_Waypoint;
                if (!em.HasComponent<VehicleTiming>(waypoint) || !em.HasComponent<RouteLane>(waypoint)) {
                    continue;
                }

                var lanes = em.GetComponentData<RouteLane>(waypoint);
                var lane  = lanes.m_EndLane != Entity.Null ? lanes.m_EndLane : lanes.m_StartLane;
                if (lane == Entity.Null || !em.HasComponent<Curve>(lane) || !em.HasComponent<Game.Net.TrackLane>(lane)) {
                    continue;
                }

                var length = em.GetComponentData<Curve>(lane).m_Length;
                if (length > 0f && length < shortest) {
                    shortest = length;
                }
            }

            return shortest < float.MaxValue ? Mathf.RoundToInt(shortest) : 0;
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
