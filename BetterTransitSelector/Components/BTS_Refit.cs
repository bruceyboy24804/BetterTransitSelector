namespace BetterTransitSelector.Components {
    #region Using Statements

    using Colossal.Serialization.Entities;

    using Unity.Entities;

    #endregion

    /// <summary>
    /// On a line (route) entity while a rolling refit runs: its running trains that differ from the built train
    /// are retired one at a time, and the depot replaces each with a train built from it.
    /// </summary>
    /// <remarks>Driven by <see cref="Systems.BTS_RefitSystem"/>; removed when the refit finishes or is stopped.</remarks>
    public struct BTS_Refit : IComponentData, IQueryTypeParameter, ISerializable {
        private const int kVersion = 1;

        /// <summary>The train being retired now; <see cref="Entity.Null"/> between trains.</summary>
        public Entity m_Train;

        /// <summary>
        /// Trains still to retire, counted when the refit starts. It bounds the refit: a replacement the depot
        /// could not rebuild (a parked train of another lead model, say) would otherwise be retired forever.
        /// </summary>
        public int m_Remaining;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(kVersion);
            writer.Write(m_Train);
            writer.Write(m_Remaining);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out int version);
            if (version >= 1) {
                reader.Read(out m_Train);
                reader.Read(out m_Remaining);
            }
        }
    }
}
