namespace BetterTransitSelector.Prefabs {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    using Game.Prefabs;
    using Game.UI.Editor;
    using Game.UI.Widgets;

    using Unity.Entities;

    #endregion

    [ComponentMenu("Better Transit Selector/", new[] { typeof(ObjectPrefab) })]
    public class BTS_Variant : ComponentBase {
        public GroupPrefab m_Placeholder;

        public int m_Index;

        public Country m_Countries = Country.None;

        [CustomField(typeof(UIIconField))]
        public string m_Icon;

        public override void GetDependencies(List<PrefabBase> prefabs) {
            base.GetDependencies(prefabs);

            if (m_Placeholder != null) {
                prefabs.Add(m_Placeholder);
            }
        }

        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            components.Add(ComponentType.ReadWrite<BTS_VariantData>());
        }

        public override void GetArchetypeComponents(HashSet<ComponentType> components) { }

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
