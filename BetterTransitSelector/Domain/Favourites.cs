namespace BetterTransitSelector.Domain {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using System.IO;

    using Colossal.PSI.Environment;

    using ModsCommon.Utils;

    #endregion

    public sealed class Favourites {
        private readonly string kPath;

        private readonly HashSet<string> m_Names = new HashSet<string>(StringComparer.Ordinal);

        private readonly PrefixedLogger m_Log = new PrefixedLogger(nameof(Favourites));

        public Favourites(string fileName) {
            kPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", "BetterTransitSelector", fileName);
        }

        public IReadOnlyCollection<string> Names => m_Names;

        public bool Contains(string prefabName) => m_Names.Contains(prefabName);

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
