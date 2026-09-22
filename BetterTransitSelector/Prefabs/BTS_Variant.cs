namespace BetterTransitSelector.Prefabs {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    using Game.Prefabs;
    using Game.UI.Editor;
    using Game.UI.Widgets;

    using Unity.Entities;

    #endregion

    /// <summary>
    /// Attached to a train to say which family of variants it belongs to.
    /// </summary>
    /// <remarks>
    /// The asset creators' design, point for point:
    ///
    /// <list type="bullet">
    /// <item>A pack — a <see cref="GroupPrefab"/> carrying <see cref="BTS_Pack"/>, duplicated
    /// from the "BTS Pack" template in the editor's asset browser and renamed — is included
    /// in the upload. It has no geometry and no behaviour, so a player without the mod just has an
    /// inert asset in the package.</item>
    /// <item>This component on each train points at that pack — the same shape as prop
    /// variations, where a master prop stands in for the set.</item>
    /// <item>An index, not a list, orders the variants, so the family stays open to future
    /// uploads.</item>
    /// <item>The same placeholder included as-is in another upload puts that upload in the same
    /// family. The asset's identity is the link: no id to type, no name to match.</item>
    /// <item>Locked by default, by construction: nobody can point at a placeholder they were not
    /// given. Collaboration is sharing the asset file.</item>
    /// </list>
    ///
    /// Nothing here is a parameter to set. The creator makes a placeholder once, attaches this to
    /// each train, and picks an index; the mod does the rest.
    /// </remarks>
    [ComponentMenu("Better Transit Selector/", new[] { typeof(ObjectPrefab) })]
    public class BTS_Variant : ComponentBase {
        /// <summary>
        /// The pack identifying this family, or empty for a train that belongs to none. Ship it in
        /// the upload; ship the same one, unchanged, in any later upload that belongs to the family.
        /// </summary>
        /// <remarks>
        /// Typed as <see cref="GroupPrefab"/>, the vanilla root type a pack is built on (see
        /// <see cref="BTS_Pack"/> for why the pack is not a prefab type of ours): the
        /// editor's picker then offers only group prefabs, so a train cannot be pointed at a prop
        /// or another train by mistake.
        ///
        /// Optional, because this component also carries <see cref="m_Countries"/>, and a train
        /// need not be in a pack to say where it runs. One component for creators to learn.
        /// </remarks>
        public GroupPrefab m_Placeholder;

        /// <summary>
        /// Sort order within the family, low first. Leave gaps so a later variant can be placed
        /// between existing ones. Ignored without a placeholder.
        /// </summary>
        public int m_Index;

        /// <summary>
        /// Where this vehicle can plausibly operate, up to five countries. Shown as flags; optional.
        /// </summary>
        /// <remarks>
        /// The creators' replacement for the theme component, which they dropped: a NightJet gets
        /// Austria and Switzerland, and a player building a Swiss city sees at a glance what fits.
        /// One multi-select in the editor; the five-country cap and the neutered select-all come
        /// from <see cref="CountryFieldBuilders"/>, not from here.
        /// </remarks>
        public Country m_Countries = Country.None;

        /// <summary>
        /// An icon for this vehicle's row in the picker, overriding the asset's own thumbnail
        /// there and only there. Empty means the thumbnail, as for any vehicle.
        /// </summary>
        /// <remarks>
        /// The asset's UI Object icon is what every game UI shows and is the right default. This
        /// exists for creators who want the picker to show something else -- a formation diagram,
        /// a livery close-up -- without changing what the rest of the game shows. Same picker as
        /// UI Object's icon.
        /// </remarks>
        [CustomField(typeof(UIIconField))]
        public string m_Icon;

        /// <inheritdoc/>
        /// <remarks>
        /// Declaring the placeholder a dependency guarantees it is loaded and has an entity by the
        /// time <see cref="LateInitialize"/> resolves it. Without this the reference can come back
        /// null and the train silently falls out of its family.
        /// </remarks>
        public override void GetDependencies(List<PrefabBase> prefabs) {
            base.GetDependencies(prefabs);

            if (m_Placeholder != null) {
                prefabs.Add(m_Placeholder);
            }
        }

        /// <inheritdoc/>
        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            components.Add(ComponentType.ReadWrite<BTS_VariantData>());
        }

        /// <inheritdoc/>
        /// <remarks>Authoring data about the prefab; a spawned vehicle has no use for it.</remarks>
        public override void GetArchetypeComponents(HashSet<ComponentType> components) { }

        /// <inheritdoc/>
        public override void LateInitialize(EntityManager entityManager, Entity entity) {
            base.LateInitialize(entityManager, entity);

            var prefabSystem = entityManager.World.GetExistingSystemManaged<PrefabSystem>();

            entityManager.SetComponentData(
                entity,
                new BTS_VariantData {
                    m_Placeholder = m_Placeholder != null ? prefabSystem.GetEntity(m_Placeholder) : Entity.Null,
                    m_Index       = m_Index,
                    m_Countries   = (ulong)m_Countries,
                });
        }
    }
}
