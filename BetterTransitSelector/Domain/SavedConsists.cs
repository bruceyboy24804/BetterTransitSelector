namespace BetterTransitSelector.Domain {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using System.IO;

    using Colossal.PSI.Environment;

    using Game.Net;

    using ModsCommon.Utils;

    #endregion

    /// <summary>One car of a saved train. By prefab name: entities do not survive a load.</summary>
    public sealed class SavedConsistCar {
        public string Prefab;
        public bool   Reversed;
    }

    /// <summary>A named train the player built, which any line on the same track type can pick.</summary>
    public sealed class SavedConsist {
        public string                Id;
        public string                Name;
        public TrackTypes            Track;
        public List<SavedConsistCar> Cars;
    }

    /// <summary>
    /// The player's train library, kept in ModsData so it is shared by every save. A line that picks a train
    /// gets a copy of its cars, so nothing here is ever needed to load a save.
    /// </summary>
    public sealed class SavedConsistStore {
        private const int kVersion = 1;

        private readonly string m_Path = Path.Combine(EnvPath.kUserDataPath, "ModsData", "BetterTransitSelector", "SavedConsists.json");
        private readonly PrefixedLogger m_Log = new PrefixedLogger(nameof(SavedConsistStore));
        private readonly List<SavedConsist> m_Configs = new List<SavedConsist>();

        /// <summary>The file exists but could not be read; it is backed up before anything replaces it.</summary>
        private bool m_Unreadable;

        public IReadOnlyList<SavedConsist> All => m_Configs;

        /// <summary>Moves on every change, so the UI can tell when to republish.</summary>
        public int Version { get; private set; }

        public bool TryGet(string id, out SavedConsist config) {
            config = m_Configs.Find(c => c.Id == id);
            return config != null;
        }

        public SavedConsist Add(string name, TrackTypes track, List<SavedConsistCar> cars) {
            var config = new SavedConsist {
                Id    = Guid.NewGuid().ToString("N"),
                Name  = string.IsNullOrWhiteSpace(name) ? $"Train {m_Configs.Count + 1}" : name.Trim(),
                Track = track,
                Cars  = cars,
            };
            m_Configs.Add(config);
            Changed();
            return config;
        }

        public bool Replace(string id, TrackTypes track, List<SavedConsistCar> cars) {
            if (!TryGet(id, out var config)) {
                return false;
            }

            config.Track = track;
            config.Cars  = cars;
            Changed();
            return true;
        }

        public bool Rename(string id, string name) {
            if (string.IsNullOrWhiteSpace(name) || !TryGet(id, out var config)) {
                return false;
            }

            config.Name = name.Trim();
            Changed();
            return true;
        }

        public bool Delete(string id) {
            if (!TryGet(id, out var config)) {
                return false;
            }

            m_Configs.Remove(config);
            Changed();
            return true;
        }

        public void Load() {
            m_Configs.Clear();
            m_Unreadable = false;
            try {
                if (!File.Exists(m_Path)) {
                    return;
                }

                var file = Newtonsoft.Json.JsonConvert.DeserializeObject<StoreFile>(File.ReadAllText(m_Path));
                if (file?.Configs == null) {
                    throw new InvalidDataException("no configs");
                }

                foreach (var config in file.Configs) {
                    if (!string.IsNullOrEmpty(config?.Id) && config.Cars != null && config.Cars.Count > 0) {
                        m_Configs.Add(config);
                    }
                }
            } catch (Exception e) {
                m_Unreadable = true;
                m_Log.Warn($"Load() -- could not read {m_Path}: {e.Message}");
            }

            Version++;
        }

        private void Changed() {
            Version++;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(m_Path));
                if (m_Unreadable) {
                    File.Copy(m_Path, m_Path + ".bad", true);
                    m_Unreadable = false;
                }

                var file = new StoreFile { Version = kVersion, Configs = m_Configs };
                File.WriteAllText(m_Path, Newtonsoft.Json.JsonConvert.SerializeObject(file, Newtonsoft.Json.Formatting.Indented));
            } catch (Exception e) {
                m_Log.Warn($"Changed() -- could not write {m_Path}: {e.Message}");
            }
        }

        // Versioned so the file's shape can change without losing players' trains.
        private sealed class StoreFile {
            public int                Version;
            public List<SavedConsist> Configs;
        }
    }
}
