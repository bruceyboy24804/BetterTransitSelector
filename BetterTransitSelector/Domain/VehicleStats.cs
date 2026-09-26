namespace BetterTransitSelector.Domain {
    #region Using Statements

    using Colossal.UI.Binding;

    using Unity.Entities;

    #endregion

    public readonly struct VehicleStats : IJsonWritable {
        public Entity Entity { get; }

        public int MaxSpeed { get; }

        public int Passengers { get; }

        public string Group { get; }

        public string GroupTitle { get; }

        public string Family { get; }

        public string FamilyTitle { get; }

        public string FamilyIcon { get; }

        public string GroupIcon { get; }

        public int Index { get; }

        public float Acceleration { get; }

        public float Braking { get; }

        public string EnergyType { get; }

        public int Carriages { get; }

        public int Length { get; }

        public string PackageDate { get; }

        public string Theme { get; }

        public string Author { get; }

        public string Countries { get; }

        public string Icon { get; }

        public int Units { get; }

        public string Collection { get; }

        public string CollectionTitle { get; }

        public string CollectionIcon { get; }

        public VehicleStats(
            Entity entity, int maxSpeed, int passengers, string group, string groupTitle,
            string family, string familyTitle, string familyIcon, string groupIcon, int index,
            float acceleration, float braking, string energyType, int carriages, int length,
            string packageDate, string theme, string author, string countries, string icon,
            string collection = "", string collectionTitle = "", string collectionIcon = "", int units = 1) {
            Entity       = entity;
            MaxSpeed     = maxSpeed;
            Passengers   = passengers;
            Group        = group;
            GroupTitle   = groupTitle;
            Family       = family;
            FamilyTitle  = familyTitle;
            FamilyIcon   = familyIcon;
            GroupIcon    = groupIcon;
            Index        = index;
            Acceleration = acceleration;
            Braking      = braking;
            EnergyType   = energyType;
            Carriages    = carriages;
            Length       = length;
            PackageDate  = packageDate;
            Theme        = theme;
            Author       = author;
            Countries    = countries;
            Icon         = icon;
            Collection      = collection;
            CollectionTitle = collectionTitle;
            CollectionIcon  = collectionIcon;
            Units           = units;
        }

        public void Write(IJsonWriter writer) {
            writer.TypeBegin("BetterTransitSelector.VehicleStats");
            writer.PropertyName("entity");
            writer.Write(Entity);
            writer.PropertyName("maxSpeed");
            writer.Write(MaxSpeed);
            writer.PropertyName("passengers");
            writer.Write(Passengers);
            writer.PropertyName("group");
            writer.Write(Group ?? string.Empty);
            writer.PropertyName("groupTitle");
            writer.Write(GroupTitle ?? string.Empty);
            writer.PropertyName("family");
            writer.Write(Family ?? string.Empty);
            writer.PropertyName("familyTitle");
            writer.Write(FamilyTitle ?? string.Empty);
            writer.PropertyName("familyIcon");
            writer.Write(FamilyIcon ?? string.Empty);
            writer.PropertyName("groupIcon");
            writer.Write(GroupIcon ?? string.Empty);
            writer.PropertyName("index");
            writer.Write(Index);
            writer.PropertyName("acceleration");
            writer.Write(Acceleration);
            writer.PropertyName("braking");
            writer.Write(Braking);
            writer.PropertyName("energyType");
            writer.Write(EnergyType ?? string.Empty);
            writer.PropertyName("carriages");
            writer.Write(Carriages);
            writer.PropertyName("length");
            writer.Write(Length);
            writer.PropertyName("packageDate");
            writer.Write(PackageDate ?? string.Empty);
            writer.PropertyName("theme");
            writer.Write(Theme ?? string.Empty);
            writer.PropertyName("author");
            writer.Write(Author ?? string.Empty);
            writer.PropertyName("countries");
            writer.Write(Countries ?? string.Empty);
            writer.PropertyName("icon");
            writer.Write(Icon ?? string.Empty);
            writer.PropertyName("units");
            writer.Write(Units);
            writer.PropertyName("collection");
            writer.Write(Collection ?? string.Empty);
            writer.PropertyName("collectionTitle");
            writer.Write(CollectionTitle ?? string.Empty);
            writer.PropertyName("collectionIcon");
            writer.Write(CollectionIcon ?? string.Empty);
            writer.TypeEnd();
        }
    }
}
