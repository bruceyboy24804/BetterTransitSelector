namespace BetterTransitSelector.Domain {
    #region Using Statements

    using Colossal.UI.Binding;

    #endregion

    public readonly struct StatOptions : IJsonWritable {
        public bool MaxSpeed { get; }
        public bool Passengers { get; }
        public bool Acceleration { get; }
        public bool Braking { get; }
        public bool EnergyType { get; }
        public bool Carriages { get; }
        public bool Length { get; }

        public string Sorting { get; }

        public int ListWidth { get; }

        public int ListHeight { get; }

        public int FavouritesWidth { get; }

        public int FavouritesHeight { get; }

        public string SpeedUnit { get; }

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
