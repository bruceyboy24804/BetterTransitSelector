namespace BetterTransitSelector.Domain {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal.UI.Binding;

    #endregion

    public sealed class AssetInfo : IJsonWritable {
        public bool   Valid;

        public string Id = string.Empty;
        public string FileName = string.Empty;
        public long   Size;
        public string Created = string.Empty;
        public string Modified = string.Empty;

        public bool   Own;

        public readonly List<(string id, int count, int passengers, float length)> Cars = new List<(string, int, int, float)>();

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
