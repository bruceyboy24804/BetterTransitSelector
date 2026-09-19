namespace BetterTransitSelector.Patches {
    #region Using Statements

    using System.Collections.Generic;

    using BetterTransitSelector.Prefabs;

    using Game.UI.Editor;

    using HarmonyLib;

    using Unity.Entities;

    #endregion

    /// <summary>
    /// Keeps packs out of the editor asset browser's generic Custom Assets and Subscribed folders.
    /// </summary>
    /// <remarks>
    /// <c>EditorAssetCategoryOverride.m_ExcludeCategories</c> looks like the tool for this and is
    /// set on every pack, but it does nothing here: <c>EditorAssetCategory.GetEntities</c> checks
    /// its <c>exclude</c> set only for entities that come out of an <c>entityQuery</c>, and these
    /// two folders are fed by a <c>getter</c> enumerable, which is yielded unfiltered. So the
    /// getters themselves are wrapped to drop anything carrying <see cref="BTS_PackData"/>. The
    /// packs still list under Better Transit Selector, which is the only place a creator looks
    /// for them.
    /// </remarks>
    [HarmonyPatch(typeof(EditorAssetCategorySystem))]
    internal static class EditorAssetCategoryPatches {
        [HarmonyPatch("GetCustomAssets")]
        [HarmonyPostfix]
        private static void GetCustomAssets(ref IEnumerable<Entity> __result) {
            __result = WithoutPacks(__result);
        }

        [HarmonyPatch("GetSubscribedAssets")]
        [HarmonyPostfix]
        private static void GetSubscribedAssets(ref IEnumerable<Entity> __result) {
            __result = WithoutPacks(__result);
        }

        private static IEnumerable<Entity> WithoutPacks(IEnumerable<Entity> source) {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            foreach (var entity in source) {
                if (!em.HasComponent<BTS_PackData>(entity)) {
                    yield return entity;
                }
            }
        }
    }
}
