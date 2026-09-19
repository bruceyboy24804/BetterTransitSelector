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
    /// The pre-0.7 pack: a prefab type of the mod's own. Kept only so packs saved with it still
    /// load while the mod is enabled; new packs are a <see cref="GroupPrefab"/> carrying
    /// <see cref="BTS_Pack"/>.
    /// </summary>
    /// <remarks>
    /// Superseded because an asset whose <em>root</em> type belongs to a mod cannot load with that
    /// mod disabled — <c>PrefabAsset.Load</c> throws <c>InvalidCastException</c> — whereas a mod
    /// component on a vanilla root is simply dropped. See <see cref="BTS_Pack"/>. Deriving
    /// from <see cref="GroupPrefab"/> lets an old pack still satisfy
    /// <see cref="BTS_Variant.m_Placeholder"/>; it does not fix the disabled-mod crash for
    /// that asset, which needs the pack re-created from the template and the trains re-pointed.
    /// No template is registered for this type, so no new asset can be made from it.
    /// </remarks>
    [Obsolete("Packs are a GroupPrefab with a BTS_Pack component since 0.7; this type loads old assets only.")]
    [ComponentMenu("Prefabs/Better Transit Selector/", new Type[] { })]
    public class BTS_TrainPackPrefab : GroupPrefab {
        /// <summary>Old home of <see cref="BTS_Pack.m_UploadIcon"/>; still honoured.</summary>
        [CustomField(typeof(UIIconField))]
        public string m_UploadIcon;

        /// <inheritdoc/>
        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            base.GetPrefabComponents(components);
            components.Add(ComponentType.ReadWrite<BTS_PackData>());
            components.Add(ComponentType.ReadWrite<BTS_PackElement>());
        }
    }
}
