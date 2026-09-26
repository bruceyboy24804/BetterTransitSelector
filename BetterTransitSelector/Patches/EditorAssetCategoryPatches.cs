namespace BetterTransitSelector.Patches {
    #region Using Statements

    using System.Collections.Generic;

    using BetterTransitSelector.Prefabs;

    using Game.UI.Editor;

    using HarmonyLib;

    using Unity.Entities;

    #endregion

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
