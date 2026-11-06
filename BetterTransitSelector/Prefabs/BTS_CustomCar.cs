namespace BetterTransitSelector.Prefabs {
    #region Using Statements

    using System.Collections.Generic;

    using Game.Prefabs;

    using Unity.Collections;
    using Unity.Entities;

    #endregion

    /// <summary>
    /// Added at runtime to every custom car (a player's copy of a train car with changed figures) so vanilla never
    /// sends one on its own: it is only ever used where the player placed it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// How: <c>VehicleSelectRequirementData.CheckRequirements</c> ORs the requirements within a group and ANDs
    /// the groups, and counts a theme as met only when it is the city's default theme, or, for a model the line
    /// has explicitly selected (<c>ignoreTheme</c>), when it is any theme. Two groups, each needing a different
    /// theme, can therefore never pass on their own (no city has two default themes) and always pass once
    /// selected. The same rule is what keeps European vehicles off North American cities.
    /// </para>
    /// <para>
    /// Done in <see cref="LateInitialize"/> rather than once on the entity because <c>PrefabSystem.UpdatePrefab</c>
    /// replaces a prefab's entity and re-runs every component's initialisation; this way the requirement comes
    /// back with it. Deliberately without a ComponentMenu: it is never authored in the editor, and it is only
    /// ever on runtime copies, which are never saved as asset files.
    /// </para>
    /// </remarks>
    public class BTS_CustomCar : ComponentBase {
        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            components.Add(ComponentType.ReadWrite<ObjectRequirementElement>());
        }

        public override void GetArchetypeComponents(HashSet<ComponentType> components) { }

        public override void LateInitialize(EntityManager entityManager, Entity entity) {
            base.LateInitialize(entityManager, entity);

            using var query  = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ThemeData>());
            using var themes = query.ToEntityArray(Allocator.Temp);
            if (themes.Length < 2) {
                // One theme only (a stripped install): nothing can exclude the car, so it stays selectable
                // like any other model rather than breaking.
                return;
            }

            // New groups numbered past everything the asset's own components added: vanilla numbers each group
            // by the buffer length at the time it adds it, so a group id is always below the current length.
            var buffer = entityManager.GetBuffer<ObjectRequirementElement>(entity);
            var group  = buffer.Length;
            buffer.Add(new ObjectRequirementElement(themes[0], group));
            buffer.Add(new ObjectRequirementElement(themes[1], group + 1));
        }
    }
}
