namespace BetterTransitSelector.Systems {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal.Entities;
    using Colossal.Serialization.Entities;

    using Game;
    using Game.Prefabs;

    using BetterTransitSelector.Domain;
    using BetterTransitSelector.Prefabs;

    using ModsCommon.Systems;

    using Unity.Collections;
    using Unity.Entities;

    #endregion

    /// <summary>
    /// Owns custom cars: copies of a train car's prefab with the player's figures (passengers, cargo, top speed,
    /// acceleration, braking), each registered as a prefab of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Why copies: those figures live on the car's prefab and are read from it by Burst jobs across the
    /// simulation (boarding, loading, train movement). A copy with other figures is simply another prefab, which
    /// every vanilla system already handles, with no system replaced. Verified live: <c>PrefabBase.Clone</c>
    /// keeps the mesh and carriage references, the copy's entity initialises like any asset's, and a train of
    /// copies renders and boards.
    /// </para>
    /// <para>
    /// Lifetime: a save names the prefabs it uses and <c>PrefabSystem.Deserialize</c> resolves them while it
    /// reads, so every custom car must be registered before a save opens. Definitions are kept in ModsData
    /// (<see cref="CustomCarStore"/>) and recreated from the main menu on, and again in
    /// <see cref="OnGamePreload"/>, which <c>GameManager</c> raises right before deserialisation. A car is never
    /// edited in place: a changed figure is a different custom car, so a car a save already uses never changes
    /// under it, and equal edits share one prefab.
    /// </para>
    /// <para>
    /// Prefab entities are never kept: <c>UpdatePrefab</c> replaces them. Only <see cref="PrefabBase"/>
    /// references are cached, and entities are resolved through <see cref="PrefabSystem"/> on every use.
    /// </para>
    /// </remarks>
    public partial class BTS_CustomCarSystem : CommonGameSystemBase {
        /// <summary>How often, in frames, pending cars are retried while their original asset has not loaded.</summary>
        private const int kRetryFrames = 60;

        private readonly CustomCarStore m_Store = new CustomCarStore();

        /// <summary>Registered custom cars by prefab name.</summary>
        private readonly Dictionary<string, PrefabBase> m_Registered = new Dictionary<string, PrefabBase>();

        /// <summary>Original assets by prefab name, filled as they are looked up.</summary>
        private readonly Dictionary<string, PrefabBase> m_Originals = new Dictionary<string, PrefabBase>();

        private PrefabSystem m_PrefabSystem;
        private EntityQuery  m_TrainPrefabQuery;
        private int          m_Frame;

        protected override void OnCreate() {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_TrainPrefabQuery = SystemAPI.QueryBuilder()
                                          .WithAll<TrainData, PrefabData>()
                                          .Build();
            m_Store.Load();
        }

        protected override void OnUpdate() {
            if (++m_Frame % kRetryFrames == 0 && HasPending()) {
                EnsureRegistered();
            }
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode) {
            base.OnGamePreload(purpose, mode);
            EnsureRegistered();
        }

        /// <summary>
        /// The prefab for <paramref name="original"/> with <paramref name="stats"/>: the original itself when no
        /// figure is changed, else the custom car, created and registered if it does not exist yet. Its entity
        /// exists at once; its data fills in on the next frame, like any newly added prefab.
        /// </summary>
        /// <param name="original">An asset's prefab entity. A custom car is followed back to its asset.</param>
        /// <param name="stats">The figures to change; zero fields keep the asset's.</param>
        public Entity GetOrCreate(Entity original, CustomCarStats stats) {
            if (TryDescribe(original, out var baseEntity, out _)) {
                original = baseEntity;
            }

            if (stats.IsEmpty) {
                return original;
            }

            var originalName = m_PrefabSystem.GetPrefabName(original);
            if (!TryGetBase(original, out var originalBase)) {
                return Entity.Null;
            }

            m_Originals[originalName] = originalBase;
            var car = m_Store.GetOrAdd(originalName, stats);
            return Register(car, originalBase, out var prefab) ? m_PrefabSystem.GetEntity(prefab) : Entity.Null;
        }

        /// <summary>
        /// Whether <paramref name="prefab"/> is a custom car, and if so its original asset's prefab entity and the
        /// figures it changes.
        /// </summary>
        public bool TryDescribe(Entity prefab, out Entity original, out CustomCarStats stats) {
            original = Entity.Null;
            stats    = default;
            if (prefab == Entity.Null || !EntityManager.Exists(prefab)
                || !m_Store.TryGet(m_PrefabSystem.GetPrefabName(prefab), out var car)
                || !TryFindOriginal(car.Original, out var originalBase)) {
                return false;
            }

            original = m_PrefabSystem.GetEntity(originalBase);
            stats    = car.Stats;
            return true;
        }

        private bool HasPending() {
            foreach (var car in m_Store.All) {
                if (!m_Registered.ContainsKey(car.Name)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Registers every stored custom car whose original asset is loaded and that is not registered.</summary>
        private void EnsureRegistered() {
            var created = 0;
            foreach (var car in m_Store.All) {
                if (!m_Registered.ContainsKey(car.Name) && TryFindOriginal(car.Original, out var originalBase)
                    && Register(car, originalBase, out _)) {
                    created++;
                }
            }

            if (created > 0) {
                m_Log.Info($"EnsureRegistered() -- registered {created} custom car(s).");
            }
        }

        /// <summary>
        /// Copies <paramref name="originalBase"/> the way <c>PrefabSystem.DuplicatePrefab</c> does, but sets the
        /// figures before <c>AddPrefab</c>, so the entity initialises once with them rather than being rebuilt.
        /// </summary>
        private bool Register(CustomCar car, PrefabBase originalBase, out PrefabBase prefab) {
            if (m_Registered.TryGetValue(car.Name, out prefab)) {
                return true;
            }

            prefab = originalBase.Clone(car.Name);
            // As DuplicatePrefab: the asset's former names belong to the asset, not to its copy.
            prefab.Remove<ObsoleteIdentifiers>();
            Apply(prefab, car.Stats);
            prefab.AddComponent<BTS_CustomCar>();

            if (!m_PrefabSystem.AddPrefab(prefab)) {
                m_Log.Warn($"Register() -- could not add '{car.Name}'.");
                prefab = null;
                return false;
            }

            m_Registered[car.Name] = prefab;
            return true;
        }

        /// <summary>Writes the changed figures onto the copy's own components, where the asset authors them.</summary>
        private static void Apply(PrefabBase prefab, CustomCarStats stats) {
            if (stats.Passengers > 0 && prefab.TryGet(out PublicTransport transport)) {
                transport.m_PassengerCapacity = stats.Passengers;
            }

            if (stats.Cargo > 0 && prefab.TryGet(out CargoTransport cargo)) {
                cargo.m_CargoCapacity = stats.Cargo;
            }

            if (prefab is TrainPrefab train) {
                if (stats.MaxSpeed > 0f) {
                    train.m_MaxSpeed = stats.MaxSpeed;
                }

                if (stats.Acceleration > 0f) {
                    train.m_Acceleration = stats.Acceleration;
                }

                if (stats.Braking > 0f) {
                    train.m_Braking = stats.Braking;
                }
            }
        }

        private bool TryGetBase(Entity prefab, out PrefabBase prefabBase) {
            prefabBase = null;
            return EntityManager.TryGetComponent(prefab, out PrefabData data)
                   && m_PrefabSystem.TryGetPrefab(data, out prefabBase);
        }

        /// <summary>Finds a loaded train asset by prefab name; null until the asset has loaded.</summary>
        private bool TryFindOriginal(string name, out PrefabBase prefabBase) {
            if (m_Originals.TryGetValue(name, out prefabBase) && prefabBase != null) {
                return true;
            }

            using var entities = m_TrainPrefabQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities) {
                if (m_PrefabSystem.GetPrefabName(entity) == name && TryGetBase(entity, out prefabBase)) {
                    m_Originals[name] = prefabBase;
                    return true;
                }
            }

            prefabBase = null;
            return false;
        }
    }
}
