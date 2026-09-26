namespace BetterTransitSelector.Domain {
    #region Using Statements

    using Colossal.UI.Binding;

    #endregion

    public readonly struct LineSummary : IJsonWritable {
        public bool Valid { get; }

        public int VehicleCount { get; }

        public int TargetCount { get; }

        public float IntervalSeconds { get; }

        public float DurationSeconds { get; }

        public int Riders { get; }

        public int CapacityNow { get; }

        public LineSummary(
            bool valid, int vehicleCount, int targetCount, float intervalSeconds, float durationSeconds,
            int riders, int capacityNow) {
            Valid           = valid;
            VehicleCount    = vehicleCount;
            TargetCount     = targetCount;
            IntervalSeconds = intervalSeconds;
            DurationSeconds = durationSeconds;
            Riders          = riders;
            CapacityNow     = capacityNow;
        }

        public void Write(IJsonWriter writer) {
            writer.TypeBegin("BetterTransitSelector.LineSummary");
            writer.PropertyName("valid");
            writer.Write(Valid);
            writer.PropertyName("vehicleCount");
            writer.Write(VehicleCount);
            writer.PropertyName("targetCount");
            writer.Write(TargetCount);
            writer.PropertyName("intervalSeconds");
            writer.Write(IntervalSeconds);
            writer.PropertyName("durationSeconds");
            writer.Write(DurationSeconds);
            writer.PropertyName("riders");
            writer.Write(Riders);
            writer.PropertyName("capacityNow");
            writer.Write(CapacityNow);
            writer.TypeEnd();
        }
    }
}
