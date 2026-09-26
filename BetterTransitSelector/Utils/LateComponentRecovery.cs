namespace BetterTransitSelector.Utils {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    using Colossal.IO.AssetDatabase;

    using Game.Prefabs;

    using ModsCommon.Utils;

    using Unity.Entities;

    using UnityEngine;

    #endregion

    /// <summary>
    /// Restores BTS components that were dropped from subscribed assets because the asset loaded before
    /// this mod's assembly did.
    /// </summary>
    /// <remarks>
    /// Why this exists: when a playset change delivers the mod and an asset together (first subscribe, or
    /// the deferred PDX database path), <c>GameManager.OnEntryIsInActivePlaysetChanged</c> adds the batch's
    /// prefabs first and only then re-initialises the mod manager, which loads new assemblies. Odin's
    /// <c>DefaultSerializationBinder</c> cannot resolve <c>BTS_Variant</c> at that moment, silently skips the
    /// component, and — the part that makes it stick — caches the failed lookup as <c>null</c> in its static
    /// <c>typeMap</c>, so every later load in the session fails too. A local copy of the mod loads at boot,
    /// before any asset, which is why it "works locally".
    /// </remarks>
    internal static class LateComponentRecovery {
        private static readonly PrefixedLogger s_Log = new PrefixedLogger(nameof(LateComponentRecovery));

        /// <summary>
        /// Registers every BTS prefab/component type with Odin by name, purges failed lookups, and only if
        /// something was purged, repairs the prefabs that loaded without their BTS components.
        /// </summary>
        /// <param name="prefabSystem">Used to rebuild the entity of every repaired prefab.</param>
        public static void Run(PrefabSystem prefabSystem) {
            var assemblyName = typeof(LateComponentRecovery).Assembly.GetName().Name;

            int purged;
            try {
                RegisterTypeBindings(assemblyName);
                purged = PurgeFailedTypeLookups(assemblyName);
            } catch (Exception e) {
                s_Log.Warn($"Run() -- could not inspect Odin's type cache: {e.Message}");
                return;
            }

            if (purged == 0) {
                return;
            }

            s_Log.Info($"Run() -- {purged} BTS type name(s) failed to resolve before the mod loaded; repairing assets.");

            try {
                var repaired = RepairLoadedPrefabs(prefabSystem, assemblyName);
                s_Log.Info($"Run() -- restored BTS components on {repaired} prefab(s).");
            } catch (Exception e) {
                s_Log.Error($"Run() -- repair failed: {e}");
            }
        }

        /// <summary>
        /// Binds each serialisable BTS type's saved name straight to its <see cref="Type"/>, so resolving it
        /// no longer depends on Odin's assembly scan having seen this DLL. Covers both name forms a .Prefab
        /// file can carry ("Full.Name, Assembly" and bare "Full.Name").
        /// </summary>
        private static void RegisterTypeBindings(string assemblyName) {
            var binder = Type.GetType("Colossal.OdinSerializer.DefaultSerializationBinder, Colossal.OdinSerializer", true);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            var bindings = (Dictionary<string, Type>)binder.GetField("customTypeNameToTypeBindings", flags).GetValue(null);
            var gate     = binder.GetField("ASSEMBLY_LOOKUP_LOCK", flags).GetValue(null);

            var types = typeof(LateComponentRecovery).Assembly.GetTypes()
                                                      .Where(t => !t.IsAbstract && typeof(ComponentBase).IsAssignableFrom(t));
            lock (gate) {
                foreach (var type in types) {
                    bindings[type.FullName + ", " + assemblyName] = type;
                    bindings[type.FullName]                       = type;
                }
            }
        }

        private static int PurgeFailedTypeLookups(string assemblyName) {
            var binder  = Type.GetType("Colossal.OdinSerializer.DefaultSerializationBinder, Colossal.OdinSerializer", true);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            var typeMap = (Dictionary<string, Type>)binder.GetField("typeMap", flags).GetValue(null);
            var gate    = binder.GetField("NAMETOTYPE_LOCK", flags).GetValue(null);

            lock (gate) {
                var stale = typeMap.Where(p => p.Value == null && p.Key.IndexOf(assemblyName, StringComparison.Ordinal) >= 0)
                                   .Select(p => p.Key)
                                   .ToList();
                foreach (var key in stale) {
                    typeMap.Remove(key);
                }

                return stale.Count;
            }
        }

        private static int RepairLoadedPrefabs(PrefabSystem prefabSystem, string assemblyName) {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var instanceField = typeof(PrefabAsset).GetField("m_Instance", flags);
            var refCountField = typeof(PrefabAsset).GetField("m_ObjectInstanceRefCount", flags);

            var marker8  = Encoding.UTF8.GetBytes(assemblyName + ".Prefabs.");
            var marker16 = Encoding.Unicode.GetBytes(assemblyName + ".Prefabs.");
            var repaired = 0;

            foreach (var asset in AssetDatabase.global.GetAssets(default(SearchFilter<PrefabAsset>)).ToList()) {
                if (asset.isBuiltin
                    || asset.state != LoadState.Object
                    || !(instanceField.GetValue(asset) is PrefabBase live)
                    || !Mentions(asset, marker8, marker16)) {
                    continue;
                }

                // Re-run the game's own deserialisation: with the ref count at 0, Load() reads the file again
                // into a new instance. The live instance and its count go straight back afterwards, so
                // nothing that holds the live prefab notices.
                var refCount = (int)refCountField.GetValue(asset);
                PrefabBase fresh;
                try {
                    refCountField.SetValue(asset, 0);
                    fresh = asset.Load() as PrefabBase;
                } finally {
                    instanceField.SetValue(asset, live);
                    refCountField.SetValue(asset, refCount);
                    live.asset = asset;
                }

                if (fresh == null || ReferenceEquals(fresh, live)) {
                    continue;
                }

                var moved = false;
                foreach (var component in fresh.components.ToList()) {
                    if (component == null
                        || component.GetType().Assembly.GetName().Name != assemblyName
                        || live.Has(component.GetType())) {
                        continue;
                    }

                    // Move the instance rather than AddComponentFrom: that copies via JsonUtility, which does
                    // not carry the GroupPrefab reference (m_Placeholder / m_Parent) at runtime.
                    fresh.components.Remove(component);
                    component.prefab = live;
                    live.components.Add(component);
                    moved = true;
                }

                UnityEngine.Object.Destroy(fresh);

                if (!moved) {
                    continue;
                }

                if (prefabSystem.TryGetEntity(live, out _)) {
                    // Rebuilds the entity with the new archetype; the new entity is Created, so
                    // PrefabInitializeSystem runs LateInitialize and BTS_VariantData gets filled.
                    prefabSystem.UpdatePrefab(live);
                }

                repaired++;
            }

            return repaired;
        }

        private static bool Mentions(PrefabAsset asset, byte[] utf8, byte[] utf16) {
            try {
                byte[] bytes;
                using (var stream = asset.GetReadStream())
                using (var memory = new MemoryStream()) {
                    stream.CopyTo(memory);
                    bytes = memory.ToArray();
                }

                return IndexOf(bytes, utf8) >= 0 || IndexOf(bytes, utf16) >= 0;
            } catch {
                return false;
            }
        }

        private static int IndexOf(byte[] haystack, byte[] needle) {
            for (var i = 0; i <= haystack.Length - needle.Length; i++) {
                var j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) {
                    j++;
                }

                if (j == needle.Length) {
                    return i;
                }
            }

            return -1;
        }
    }
}
