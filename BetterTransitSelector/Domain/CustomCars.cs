namespace BetterTransitSelector.Domain {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    using Colossal.PSI.Environment;

    using ModsCommon.Utils;

    #endregion

    /// <summary>
    /// The figures a player can change on one train car. Every field at 0 keeps the original asset's value.
    /// </summary>
    public struct CustomCarStats : IEquatable<CustomCarStats> {
        public int   Passengers;
        public int   Cargo;

        /// <summary>km/h, the unit <c>TrainPrefab.m_MaxSpeed</c> is authored in.</summary>
        public float MaxSpeed;

        /// <summary>m/s².</summary>
        public float Acceleration;

        /// <summary>m/s².</summary>
        public float Braking;

        [Newtonsoft.Json.JsonIgnore]
        public bool IsEmpty => Passengers <= 0 && Cargo <= 0 && MaxSpeed <= 0f && Acceleration <= 0f && Braking <= 0f;

        /// <summary>Sets one figure by the name the UI uses; 0 or less clears it back to the asset's.</summary>
        /// <returns>False for an unknown name.</returns>
        public bool Set(string stat, float value) {
            var v = Math.Max(0f, value);
            switch (stat) {
                case "passengers":   Passengers   = (int)Math.Round(v); return true;
                case "cargo":        Cargo        = (int)Math.Round(v); return true;
                case "maxSpeed":     MaxSpeed     = v; return true;
                case "acceleration": Acceleration = v; return true;
                case "braking":      Braking      = v; return true;
                default:             return false;
            }
        }

        /// <summary>
        /// A stable text form, the input to the variant's name. Figures are rounded to what the UI can set,
        /// so float noise never makes two equal edits two variants.
        /// </summary>
        public string Key() =>
            string.Format(CultureInfo.InvariantCulture, "p{0}c{1}s{2:0.#}a{3:0.##}b{4:0.##}",
                Passengers, Cargo, MaxSpeed, Acceleration, Braking);

        public bool Equals(CustomCarStats other) => Key() == other.Key();

        public override bool Equals(object obj) => obj is CustomCarStats other && Equals(other);

        public override int GetHashCode() => Key().GetHashCode();
    }

    /// <summary>One customised car: an asset's prefab plus changed figures, registered as a prefab of its own.</summary>
    public sealed class CustomCar {
        /// <summary>Marks a variant's prefab name; everything before it is the original asset's name.</summary>
        public const string kSeparator = "#BTS:";

        /// <summary>Prefab name of the asset the variant copies.</summary>
        public string Original;

        public CustomCarStats Stats;

        /// <summary>
        /// The variant's prefab name, "&lt;original&gt;#BTS:&lt;hash of the figures&gt;". It is derived, never stored:
        /// saves find prefabs by (type, name), so the same original and figures must always give the same name,
        /// on every machine and in every session.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string Name => NameFor(Original, Stats);

        public static string NameFor(string original, CustomCarStats stats) =>
            original + kSeparator + Fnv1a(original + "|" + stats.Key()).ToString("x8");

        /// <summary>The original asset's name when <paramref name="prefabName"/> is a variant's, else null.</summary>
        public static string OriginalOf(string prefabName) {
            var at = prefabName?.IndexOf(kSeparator, StringComparison.Ordinal) ?? -1;
            return at > 0 ? prefabName.Substring(0, at) : null;
        }

        // A fixed hash, not string.GetHashCode: the name has to be identical across runs and machines.
        private static uint Fnv1a(string text) {
            var hash = 2166136261u;
            foreach (var b in Encoding.UTF8.GetBytes(text)) {
                hash ^= b;
                hash *= 16777619u;
            }

            return hash;
        }
    }

    /// <summary>
    /// Every car variant the player has made, kept in ModsData rather than in a save: a save names its prefabs,
    /// and <c>PrefabSystem.Deserialize</c> resolves those names while it reads, so a variant must already be
    /// registered before any save that uses it is opened.
    /// </summary>
    public sealed class CustomCarStore {
        private const int kVersion = 1;

        private readonly string m_Path = Path.Combine(EnvPath.kUserDataPath, "ModsData", "BetterTransitSelector", "CustomCars.json");
        private readonly PrefixedLogger m_Log = new PrefixedLogger(nameof(CustomCarStore));
        private readonly Dictionary<string, CustomCar> m_ByName = new Dictionary<string, CustomCar>(StringComparer.Ordinal);

        public IEnumerable<CustomCar> All => m_ByName.Values;

        public bool TryGet(string prefabName, out CustomCar variant) => m_ByName.TryGetValue(prefabName ?? string.Empty, out variant);

        /// <summary>Records a variant and saves the file when it is new. Returns the stored one either way.</summary>
        public CustomCar GetOrAdd(string original, CustomCarStats stats) {
            var name = CustomCar.NameFor(original, stats);
            if (m_ByName.TryGetValue(name, out var existing)) {
                return existing;
            }

            var variant = new CustomCar { Original = original, Stats = stats };
            m_ByName[name] = variant;
            Save();
            return variant;
        }

        public void Load() {
            m_ByName.Clear();
            try {
                if (!File.Exists(m_Path)) {
                    return;
                }

                var file = Newtonsoft.Json.JsonConvert.DeserializeObject<StoreFile>(File.ReadAllText(m_Path));
                if (file?.Cars == null) {
                    return;
                }

                foreach (var car in file.Cars) {
                    if (!string.IsNullOrEmpty(car?.Original) && !car.Stats.IsEmpty) {
                        m_ByName[car.Name] = car;
                    }
                }
            } catch (Exception e) {
                m_Log.Warn($"Load() -- could not read {m_Path}: {e.Message}");
            }
        }

        private void Save() {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(m_Path));
                var file = new StoreFile { Version = kVersion, Cars = new List<CustomCar>(m_ByName.Values) };
                File.WriteAllText(m_Path, Newtonsoft.Json.JsonConvert.SerializeObject(file, Newtonsoft.Json.Formatting.Indented));
            } catch (Exception e) {
                m_Log.Warn($"Save() -- could not write {m_Path}: {e.Message}");
            }
        }

        // Versioned so the file's shape can change without losing players' variants.
        private sealed class StoreFile {
            public int              Version;
            public List<CustomCar> Cars;
        }
    }
}
