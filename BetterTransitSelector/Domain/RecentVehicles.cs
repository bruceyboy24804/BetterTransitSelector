namespace BetterTransitSelector.Domain {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using System.IO;

    using Colossal.PSI.Environment;

    using ModsCommon.Utils;

    #endregion

    /// <summary>
    /// The last few vehicles the player picked, newest first, persisted across sessions.
    /// </summary>
    /// <remarks>
    /// Keyed by prefab name for the same reason as <see cref="Favourites"/>. Recorded on select
    /// only -- deselecting a vehicle is not "using" it -- and capped at three: the point is the
    /// handful you are actively assigning across lines right now, not a history. Stored next to
    /// the favourites file, same shape.
    /// </remarks>
    public sealed class RecentVehicles {
        /// <summary>How many to keep. Three: what fits "above the fold" without becoming a list of its own.</summary>
        public const int kCapacity = 3;

        private static readonly string kPath = Path.Combine(
            EnvPath.kUserDataPath, "ModsData", "BetterTransitSelector", "Recent.json");

        private readonly List<string> m_Names = new List<string>(kCapacity);

        private readonly PrefixedLogger m_Log = new PrefixedLogger(nameof(RecentVehicles));

        /// <summary>Newest first.</summary>
        public IReadOnlyList<string> Names => m_Names;

        /// <summary>Moves or inserts one vehicle at the front, dropping the oldest past the cap.</summary>
        public void Record(string prefabName) {
            if (string.IsNullOrEmpty(prefabName)) {
                return;
            }

            m_Names.Remove(prefabName);
            m_Names.Insert(0, prefabName);
            while (m_Names.Count > kCapacity) {
                m_Names.RemoveAt(m_Names.Count - 1);
            }

            Save();
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
                    if (!string.IsNullOrEmpty(name) && !m_Names.Contains(name) && m_Names.Count < kCapacity) {
                        m_Names.Add(name);
                    }
                }
            } catch (Exception e) {
                m_Log.Warn($"Could not read recent vehicles from {kPath}: {e.Message}");
            }
        }

        private void Save() {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(kPath));
                File.WriteAllText(kPath, Newtonsoft.Json.JsonConvert.SerializeObject(m_Names, Newtonsoft.Json.Formatting.Indented));
            } catch (Exception e) {
                m_Log.Warn($"Could not write recent vehicles to {kPath}: {e.Message}");
            }
        }
    }
}
