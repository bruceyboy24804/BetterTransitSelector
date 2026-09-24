namespace BetterTransitSelector.Domain {
    #region Using Statements

    using Colossal.UI.Binding;

    #endregion

    /// <summary>
    /// The selected transport line's fleet figures, for the summary row under the picker.
    /// </summary>
    /// <remarks>
    /// Written for every selection; <see cref="Valid"/> is false when the selected entity is
    /// not a transport line, and the UI shows nothing.
    ///
    /// The fleet the line requests is the game's own arithmetic, mirrored from
    /// <c>VehicleCountSection.CalculateVehicleCountJob</c>: <c>round(stableDuration / interval)</c>,
    /// where the duration is the sum of the route's segment path durations plus stop time. The
    /// vehicle mix plays no part in it -- <c>RoutePathSystem</c> plans the route at a 1000 km/h
    /// cap, so track speed limits set the duration, not the vehicle -- which is why this row
    /// reports the fleet as a fact about the line and puts the mix only into the capacity figures.
    /// </remarks>
    public readonly struct LineSummary : IJsonWritable {
        public bool Valid { get; }

        /// <summary>Vehicles currently on the line.</summary>
        public int VehicleCount { get; }

        /// <summary>Vehicles the line wants, given its interval and duration.</summary>
        public int TargetCount { get; }

        /// <summary>Seconds between departures, after policy modifiers.</summary>
        public float IntervalSeconds { get; }

        /// <summary>Seconds for one full loop, stops included.</summary>
        public float DurationSeconds { get; }

        /// <summary>Passengers aboard the line's vehicles right now, summed over the fleet.</summary>
        /// <remarks>
        /// The game's own demand figure: <c>TransportUIUtils</c> sums each live vehicle's cargo
        /// and capacity, and the transport overview's "usage" is their ratio. Using the same
        /// numbers means our load matches the overview's.
        /// </remarks>
        public int Riders { get; }

        /// <summary>Seats aboard the line's vehicles right now, summed over the fleet.</summary>
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

        /// <inheritdoc/>
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
