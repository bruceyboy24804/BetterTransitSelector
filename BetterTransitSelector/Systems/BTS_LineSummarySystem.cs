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

    public partial class BTS_LineSummarySystem : CommonUISystemBase {
        protected override string ModId => Mod.Instance.Id;

        private SelectedInfoUISystem m_SelectedInfo;

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

            var riders   = 0;
            var capacity = 0;
            var count    = TransportUIUtils.GetRouteVehiclesCount(em, line, ref riders, ref capacity);

            return new LineSummary(true, count, target, interval, duration, riders, capacity);
        }

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
