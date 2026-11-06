namespace BetterTransitSelector.Components {
    #region Using Statements

    using Colossal.Serialization.Entities;

    using Unity.Entities;

    #endregion

    /// <summary>
    /// One car of a player-built train, in order from the front. The buffer lives on the line (route)
    /// entity; no buffer, or an empty one, means the line runs vanilla consists.
    /// </summary>
    /// <remarks>
    /// Read by <see cref="Systems.BTS_ConsistSystem"/> each time the line's depot builds a new train, and
    /// edited by <see cref="Systems.BTS_ConsistBuilderSystem"/>. Trains already running are never touched.
    /// </remarks>
    [InternalBufferCapacity(0)]
    public struct BTS_ConsistCar : IBufferElementData, ISerializable {
        // 1: prefab, reversed.
        // 2: + three per-car driving-figure floats. Written only by an unreleased build that replaced
        //    TrainNavigationSystem; superseded by custom cars (a customised car is its own prefab), so
        //    version 2 is still read, its floats discarded.
        // 3: prefab, reversed (a custom car is simply its own prefab, see BTS_CustomCarSystem).
        private const int kVersion = 3;

        /// <summary>The car's vehicle prefab: an asset's own prefab, or a BTS custom car made from it.</summary>
        public Entity m_Prefab;

        /// <summary>True when the car runs backwards (cab or tender facing the rear).</summary>
        public bool m_Reversed;

        /// <summary>Creates a car slot.</summary>
        /// <param name="prefab">The car's vehicle prefab.</param>
        /// <param name="reversed">Whether the car faces the rear.</param>
        public BTS_ConsistCar(Entity prefab, bool reversed) {
            m_Prefab   = prefab;
            m_Reversed = reversed;
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(kVersion);
            writer.Write(m_Prefab);
            writer.Write(m_Reversed);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out int version);
            if (version >= 1) {
                reader.Read(out m_Prefab);
                reader.Read(out m_Reversed);
            }

            if (version == 2) {
                reader.Read(out float _);
                reader.Read(out float _);
                reader.Read(out float _);
            }
        }
    }
}
