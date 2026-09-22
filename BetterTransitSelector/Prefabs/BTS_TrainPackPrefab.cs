namespace BetterTransitSelector.Prefabs {
    #region Using Statements

    using System;

    using Game.Prefabs;

    #endregion

    /// <summary>
    /// The pre-0.7 pack type, kept as an empty shell so an asset saved with it still deserialises
    /// while the mod is enabled. It does nothing: no fields, no ECS components.
    /// </summary>
    /// <remarks>
    /// Removing the type outright made every old pack fail to load for everyone
    /// (<c>InvalidCastException</c> in <c>PrefabAsset.Load</c>, the same error players with the
    /// mod disabled already see). Keeping it empty means: mod on, the asset loads as an inert
    /// group and the variants pointing at it still form a family by its GUID; mod off, it fails
    /// as before. Either way the creator has to recreate the pack from the BTS Pack template, and
    /// <c>BTS_VehicleStatsSystem</c> logs which packages still carry one. Its fields were dropped,
    /// so an Upload Icon set on an old pack is not read.
    /// </remarks>
    [Obsolete("Packs are a GroupPrefab with a BTS_Pack component since 0.7; this shell only lets old assets load.")]
    public class BTS_TrainPackPrefab : GroupPrefab { }
}
