namespace BetterTransitSelector.Systems {
    #region Using Statements

    using Game.Prefabs;

    using BetterTransitSelector.Utils;

    using ModsCommon.Systems;

    #endregion

    /// <summary>
    /// Makes the BTS prefab components (<c>BTS_Variant</c>, <c>BTS_Pack</c>, …) known to the prefab
    /// deserialiser as soon as the mod's systems exist, and recovers any asset that loaded without them.
    /// </summary>
    /// <remarks>
    /// All the work is in <see cref="OnCreate"/>. This alone cannot beat the load-order race: a playset
    /// update loads the asset's prefabs before it loads this DLL, and those components are already gone by
    /// the time any system is created. That is why <see cref="LateComponentRecovery"/> also purges Odin's
    /// cached failures and re-reads the affected prefabs. See its remarks for the full story.
    /// </remarks>
    public partial class BTS_PrefabComponentSystem : CommonGameSystemBase {
        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            LateComponentRecovery.Run(World.GetOrCreateSystemManaged<PrefabSystem>());

            // Nothing to do per frame.
            Enabled = false;
        }

        /// <inheritdoc/>
        protected override void OnUpdate() { }
    }
}
