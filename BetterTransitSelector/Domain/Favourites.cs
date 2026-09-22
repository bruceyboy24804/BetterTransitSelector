namespace BetterTransitSelector.Domain {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using System.IO;

    using Colossal.PSI.Environment;

    using ModsCommon.Utils;

    #endregion

    /// <summary>
    /// The player's starred vehicles, persisted across sessions and saves.
    /// </summary>
    /// <remarks>
    /// Keyed by prefab name, never by entity: prefab entities are recreated on every load, so an
    /// entity-keyed set would point at nothing by the next session. The name is what vanilla's
    /// own selector writes as the row's <c>id</c>, so the UI can join on it with no extra lookup.
    ///
    /// Stored as a plain JSON array in the mod's ModsData folder -- the same shape and place Find
    /// It keeps its favourites -- so it survives a mod update, is not tied to any one save, and
    /// can be hand-edited or deleted to reset.
    /// </remarks>
    public sealed class Favourites {
        private readonly string kPath;

        private readonly HashSet<string> m_Names = new HashSet<string>(StringComparer.Ordinal);

        private readonly PrefixedLogger m_Log = new PrefixedLogger(nameof(Favourites));

        /// <param name="fileName">
        /// The file under ModsData/BetterTransitSelector. One instance per kind of key -- vehicle
        /// names in one file, pack ids in another -- so the two sets can never collide.
        /// </param>
        public Favourites(string fileName) {
            kPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", "BetterTransitSelector", fileName);
        }

        /// <summary>The starred prefab names, in no particular order.</summary>
        public IReadOnlyCollection<string> Names => m_Names;

        public bool Contains(string prefabName) => m_Names.Contains(prefabName);

        /// <summary>Stars or unstars one vehicle and writes the file. Returns the new state.</summary>
        public bool Toggle(string prefabName) {
            if (string.IsNullOrEmpty(prefabName)) {
                return false;
            }

            var on = !m_Names.Remove(prefabName);
            if (on) {
                m_Names.Add(prefabName);
            }

            Save();
            return on;
        }

        public void Load() {
            m_Names.Clear();
            try {
                if (!File.Exists(kPath)) {
                    return;
                }

                var names = Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(File.ReadAllText(kPath));
                if (names == null) {
                    return;
                }

                foreach (var name in names) {
                    if (!string.IsNullOrEmpty(name)) {
                        m_Names.Add(name);
                    }
                }
            } catch (Exception e) {
                // A corrupt file loses the stars, not the game: log and start empty.
                m_Log.Warn($"Could not read favourites from {kPath}: {e.Message}");
            }
        }

        private void Save() {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(kPath));
                File.WriteAllText(kPath, Newtonsoft.Json.JsonConvert.SerializeObject(m_Names, Newtonsoft.Json.Formatting.Indented));
            } catch (Exception e) {
                m_Log.Warn($"Could not write favourites to {kPath}: {e.Message}");
            }
        }
    }
}
