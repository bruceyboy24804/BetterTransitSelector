namespace BetterTransitSelector.Prefabs {
    #region Using Statements

    using System.Collections.Generic;

    using Game.Prefabs;
    using Game.UI.Editor;
    using Game.UI.Widgets;

    using Unity.Entities;

    #endregion

    [ComponentMenu("Better Transit Selector/", new[] { typeof(GroupPrefab) })]
    public class BTS_Pack : ComponentBase {
        [CustomField(typeof(UIIconField))]
        public string m_UploadIcon;

        public GroupPrefab m_Parent;

        public string m_DisplayName;

        public bool m_Exclusive;

        public override void GetDependencies(List<PrefabBase> prefabs) {
            base.GetDependencies(prefabs);

            if (m_Parent != null) {
                prefabs.Add(m_Parent);
            }
        }

        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            components.Add(ComponentType.ReadWrite<BTS_PackData>());

            components.Add(ComponentType.ReadWrite<BTS_PackElement>());
        }

        public override void GetArchetypeComponents(HashSet<ComponentType> components) { }
    }
}
