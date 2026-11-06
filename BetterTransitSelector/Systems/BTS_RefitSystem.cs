namespace BetterTransitSelector.Systems {
    #region Using Statements

    using Colossal.Entities;

    using Game.Routes;
    using Game.Vehicles;

    using BetterTransitSelector.Components;
    using BetterTransitSelector.Domain;

    using ModsCommon.Systems;

    using Unity.Collections;
    using Unity.Entities;

    #endregion

    /// <summary>
    /// Runs rolling refits (<see cref="BTS_Refit"/>): a line's running trains that differ from its built train are
    /// retired one at a time, and the depot replaces each with a train built from it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Retiring uses vanilla's own route-abandon: <see cref="PublicTransportFlags.AbandonRoute"/> is what
    /// <c>TransportLineSystem</c> sets on a train whose models no longer match its line. The train finishes at a
    /// stop, leaves the line and returns to its depot; the line then requests a replacement.
    /// </para>
    /// <para>
    /// Why the flag is re-applied every update: <c>TransportLineSystem</c> also cancels it. When a line has fewer
    /// non-retiring trains than it wants, <c>CancelAbandon</c> clears it on any train that still passes
    /// <c>RouteUtils.CheckVehicleModel</c>, and a train of the right models with the old cars does pass. This
    /// system is ordered straight after <c>TransportLineSystem</c>, so the flag is back before anything acts on it.
    /// </para>
    /// <para>
    /// One train at a time keeps the line within one train of its capacity, instead of emptying it until the depot
    /// catches up.
    /// </para>
    /// </remarks>
    public partial class BTS_RefitSystem : CommonGameSystemBase {
        private EntityQuery m_RefitQuery;

        protected override void OnCreate() {
            base.OnCreate();
            m_RefitQuery = SystemAPI.QueryBuilder()
                                    .WithAllRW<BTS_Refit>()
                                    .WithAll<RouteVehicle>()
                                    .Build();
            RequireForUpdate(m_RefitQuery);
        }

        protected override void OnUpdate() {
            using var lines = m_RefitQuery.ToEntityArray(Allocator.Temp);
            foreach (var line in lines) {
                Step(line);
            }
        }

        private void Step(Entity line) {
            var em    = EntityManager;
            var refit = em.GetComponentData<BTS_Refit>(line);

            // A line whose built train was reset has nothing to refit to.
            if (!em.TryGetBuffer(line, true, out DynamicBuffer<BTS_ConsistCar> build) || build.Length == 0) {
                Finish(line, "the line no longer has a built train");
                return;
            }

            // Still retiring one: hold its flag against vanilla's cancel until it has left the line.
            if (refit.m_Train != Entity.Null && OnLine(refit.m_Train, line)) {
                SetAbandon(refit.m_Train);
                return;
            }

            if (refit.m_Remaining <= 0) {
                Finish(line, "every counted train was replaced");
                return;
            }

            using var cars = build.ToNativeArray(Allocator.Temp);
            var next = ConsistRules.MostWornMismatch(em, line, cars);
            if (next == Entity.Null) {
                Finish(line, "every train matches the built train");
                return;
            }

            SetAbandon(next);
            refit.m_Train = next;
            refit.m_Remaining--;
            em.SetComponentData(line, refit);
        }

        private bool OnLine(Entity train, Entity line) =>
            EntityManager.Exists(train)
            && EntityManager.TryGetComponent(train, out CurrentRoute route)
            && route.m_Route == line;

        private void SetAbandon(Entity train) {
            if (!EntityManager.TryGetComponent(train, out PublicTransport transport)
                || (transport.m_State & PublicTransportFlags.AbandonRoute) != 0) {
                return;
            }

            transport.m_State |= PublicTransportFlags.AbandonRoute;
            EntityManager.SetComponentData(train, transport);
        }

        private void Finish(Entity line, string why) {
            EntityManager.RemoveComponent<BTS_Refit>(line);
            m_Log.Info($"Step() -- refit of line {line.Index}:{line.Version} done: {why}.");
        }
    }
}
