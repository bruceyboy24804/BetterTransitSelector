namespace BetterTransitSelector.Domain {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal.UI.Binding;

    #endregion

    /// <summary>
    /// What the game knows about one vehicle asset, for the picker's vehicle page: the file it
    /// came from and the consist behind the headline figures.
    /// </summary>
    /// <remarks>
    /// The counterpart of <see cref="PackInfo"/> one tier down. There is no per-asset entry in
    /// the mod-browser cache -- PDX publishes uploads, not assets -- so everything here is read
    /// from the asset database's own metadata and the prefab's carriage list. The stats the row
    /// already carries (speed, capacity, length, countries, icon) are not repeated; the UI joins
    /// them from the stats table.
    /// </remarks>
    public sealed class AssetInfo : IJsonWritable {
        public bool   Valid;
        /// <summary>The prefab name the page was requested for, so the UI knows the answer is its own.</summary>
        public string Id = string.Empty;
        public string FileName = string.Empty;
        public long   Size;
        public string Created = string.Empty;
        public string Modified = string.Empty;
        /// <summary>Whether the asset is the player's own (editor-made) rather than subscribed.</summary>
        public bool   Own;
        /// <summary>Every car in the consist in running order: prefab name, count, capacity, length in metres.</summary>
        public readonly List<(string id, int count, int passengers, float length)> Cars = new List<(string, int, int, float)>();

        /// <inheritdoc/>
        public void Write(IJsonWriter writer) {
            writer.TypeBegin("BetterTransitSelector.AssetInfo");
            writer.PropertyName("valid");    writer.Write(Valid);
            writer.PropertyName("id");       writer.Write(Id);
            writer.PropertyName("fileName"); writer.Write(FileName);
            writer.PropertyName("size");     writer.Write(Size);
            writer.PropertyName("created");  writer.Write(Created);
            writer.PropertyName("modified"); writer.Write(Modified);
            writer.PropertyName("own");      writer.Write(Own);

            writer.PropertyName("cars");
            writer.ArrayBegin(Cars.Count);
            foreach (var (id, count, passengers, length) in Cars) {
                writer.TypeBegin("BetterTransitSelector.AssetCar");
                writer.PropertyName("id");         writer.Write(id);
                writer.PropertyName("count");      writer.Write(count);
                writer.PropertyName("passengers"); writer.Write(passengers);
                writer.PropertyName("length");     writer.Write(length);
                writer.TypeEnd();
            }
            writer.ArrayEnd();

            writer.TypeEnd();
        }
    }
}
