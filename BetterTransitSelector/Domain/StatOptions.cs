namespace BetterTransitSelector.Domain {
    #region Using Statements

    using Colossal.UI.Binding;

    #endregion

    /// <summary>
    /// Which stats the rows should show, mirrored from the mod's settings.
    /// </summary>
    /// <remarks>
    /// Sent as its own binding rather than baked into each row's stats: the values are the same for
    /// every vehicle, and the stats table is rebuilt only on load, whereas these change the moment
    /// the player touches a checkbox. Keeping them apart lets the UI re-render immediately without
    /// the table being recomputed.
    /// </remarks>
    public readonly struct StatOptions : IJsonWritable {
        public bool MaxSpeed { get; }
        public bool Passengers { get; }
        public bool Acceleration { get; }
        public bool Braking { get; }
        public bool EnergyType { get; }
        public bool Carriages { get; }
        public bool Length { get; }

        /// <summary>The chosen order, as its enum name so the UI reads it without a shared numbering.</summary>
        public string Sorting { get; }

        /// <summary>Minimum width of the open list in UI units; 0 means fit to contents.</summary>
        public int ListWidth { get; }

        public int ListHeight { get; }

        public int FavouritesWidth { get; }

        public int FavouritesHeight { get; }

        /// <summary>"Auto" | "Kph" | "Mph" -- the enum name, so the UI reads it without a shared numbering.</summary>
        public string SpeedUnit { get; }

        /// <summary>Whether pack headers offer the info button that opens the upload's page.</summary>
        public bool RecentOpen { get; }

        public bool PackPages { get; }

        public bool VehiclePages { get; }

        public StatOptions(Setting setting) {
            Sorting      = setting.Sorting.ToString();
            ListWidth    = setting.ListWidth;
            ListHeight       = setting.ListHeight;
            FavouritesWidth  = setting.FavouritesWidth;
            FavouritesHeight = setting.FavouritesHeight;
            SpeedUnit    = setting.Speed.ToString();
            RecentOpen   = setting.RecentOpen;
            PackPages    = setting.PackPages;
            VehiclePages = setting.VehiclePages;
            MaxSpeed     = setting.ShowMaxSpeed;
            Passengers   = setting.ShowPassengers;
            Acceleration = setting.ShowAcceleration;
            Braking      = setting.ShowBraking;
            EnergyType   = setting.ShowEnergyType;
            Carriages    = setting.ShowCarriages;
            Length       = setting.ShowLength;
        }

        /// <inheritdoc/>
        public void Write(IJsonWriter writer) {
            writer.TypeBegin("BetterTransitSelector.StatOptions");
            writer.PropertyName("maxSpeed");
            writer.Write(MaxSpeed);
            writer.PropertyName("passengers");
            writer.Write(Passengers);
            writer.PropertyName("acceleration");
            writer.Write(Acceleration);
            writer.PropertyName("braking");
            writer.Write(Braking);
            writer.PropertyName("energyType");
            writer.Write(EnergyType);
            writer.PropertyName("carriages");
            writer.Write(Carriages);
            writer.PropertyName("length");
            writer.Write(Length);
            writer.PropertyName("sorting");
            writer.Write(Sorting ?? "Default");
            writer.PropertyName("listWidth");
            writer.Write(ListWidth);
            writer.PropertyName("listHeight");
            writer.Write(ListHeight);
            writer.PropertyName("favouritesWidth");
            writer.Write(FavouritesWidth);
            writer.PropertyName("favouritesHeight");
            writer.Write(FavouritesHeight);
            writer.PropertyName("speedUnit");
            writer.Write(SpeedUnit ?? "Auto");
            writer.PropertyName("recentOpen");
            writer.Write(RecentOpen);
            writer.PropertyName("packPages");
            writer.Write(PackPages);
            writer.PropertyName("vehiclePages");
            writer.Write(VehiclePages);
            writer.TypeEnd();
        }
    }
}
