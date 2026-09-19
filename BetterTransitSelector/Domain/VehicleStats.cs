namespace BetterTransitSelector.Domain {
    #region Using Statements

    using Colossal.UI.Binding;

    using Unity.Entities;

    #endregion

    /// <summary>
    /// Display stats for one vehicle prefab, keyed by the same prefab entity vanilla's
    /// <c>SelectVehiclesSection+VehiclePrefab</c> payload writes — so the UI can join the two.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="IJsonWritable"/> rather than relying on reflection: GenericUIWriter
    /// checks for it first, and an explicit shape keeps the property names stable and lowercase to
    /// match the vanilla payload's convention.
    /// </remarks>
    public readonly struct VehicleStats : IJsonWritable {
        /// <summary>The vehicle prefab entity. The join key with vanilla's list.</summary>
        public Entity Entity { get; }

        /// <summary>Top speed in km/h, already converted and rounded. 0 when unknown.</summary>
        public int MaxSpeed { get; }

        /// <summary>Passenger capacity. 0 when the prefab carries no passenger data.</summary>
        public int Passengers { get; }

        /// <summary>
        /// The package this vehicle was published in, by prefab name; empty when it has none.
        /// </summary>
        /// <remarks>
        /// This is the game's own packaging relation — everything published in one upload shares a
        /// content prerequisite prefab named <c>Mod:&lt;pdxId&gt;</c>. Measured live on a modded
        /// load order: 208 of 231 vehicle prefabs carry it, against 1 for <c>AssetPackElement</c>,
        /// which turns out to be Colossal's DLC-bundling mechanism rather than anything creators
        /// use. Asset packs remain the fallback so DLC bundles still group.
        ///
        /// One package per vehicle, so unlike asset packs this is not many-to-many and the UI needs
        /// no rule for picking between several.
        /// </remarks>
        public string Group { get; }

        /// <summary>
        /// Headline for the group, taken from the package's parent asset; empty when it has none.
        /// </summary>
        /// <remarks>
        /// The parent is the asset in the package that is <em>not</em> loadout-eligible — it carries
        /// no <c>PublicTransportVehicleData</c>, so the vanilla selector never lists it, and the
        /// assets that are listed are its variants. That is why the headline cannot be found among
        /// the entries vanilla sends the UI: by construction the parent is not one of them.
        ///
        /// Today most packages ship no such asset and this comes back empty. It is written this way
        /// for the placeholder "train pack" prefab creators are expected to ship — the moment one
        /// appears in a package, it becomes that group's headline with no further change here.
        /// </remarks>
        public string GroupTitle { get; }

        /// <summary>
        /// The placeholder this train was declared a variant of, by asset GUID; empty when the
        /// creator declared none.
        /// </summary>
        /// <remarks>
        /// This is the tier <em>inside</em> the group, never a replacement for it. An earlier
        /// version put the placeholder into <see cref="Group"/>, which pulled a declared train out
        /// of its own upload: the tagged BR612 and its untagged sibling from the same .cok landed in
        /// two different groups, each of one member, and the UI folded both to loose rows. The
        /// upload is the group; the placeholder is a family within it; the index orders the family.
        /// </remarks>
        public string Family { get; }

        /// <summary>The placeholder's name, the family's heading; empty with no family.</summary>
        public string FamilyTitle { get; }

        /// <summary>
        /// Icon URL for the family header: the pack prefab's own <c>UIObject</c> icon when the
        /// creator gave it one, else empty and the UI shows the first member's thumbnail.
        /// </summary>
        public string FamilyIcon { get; }

        /// <summary>
        /// Icon URL for the upload's group header: the icon of the upload's single pack prefab
        /// when it has exactly one with an icon, else empty. An upload has no asset of its own to
        /// carry an icon, so this is the nearest thing a creator can control.
        /// </summary>
        public string GroupIcon { get; }

        /// <summary>
        /// Sort order within the family, low first; <see cref="int.MaxValue"/> when unset.
        /// </summary>
        /// <remarks>
        /// An index rather than a list position, at the creators' request: a list would fix the set
        /// of variants at the moment the first one is published, whereas an index lets a livery
        /// uploaded later slot into place with nothing existing needing to be republished.
        /// </remarks>
        public int Index { get; }

        /// <summary>Acceleration in m/s². 0 when the prefab carries none.</summary>
        public float Acceleration { get; }

        /// <summary>Braking in m/s². 0 when the prefab carries none.</summary>
        public float Braking { get; }

        /// <summary>What the vehicle runs on, e.g. "Electricity". Empty when unknown.</summary>
        public string EnergyType { get; }

        /// <summary>
        /// Carriages the consist can run to, summed over the prefab's carriage list; 0 for a single
        /// unit.
        /// </summary>
        public int Carriages { get; }

        /// <summary>Length in whole metres, from the prefab's own geometry. 0 when unknown.</summary>
        public int Length { get; }

        /// <summary>
        /// When the upload was published, ISO 8601 UTC; empty when unknown. Sorts as a string.
        /// </summary>
        public string PackageDate { get; }

        /// <summary>
        /// Theme this vehicle belongs to, e.g. "EU"; empty when theme-agnostic. The selector shows
        /// every theme at once because vanilla lists with ignoreTheme, so this is what narrows it.
        /// </summary>
        public string Theme { get; }

        /// <summary>Creator who published the upload; empty when unknown.</summary>
        public string Author { get; }

        /// <summary>
        /// Countries the creator declared the vehicle operates in, as comma-separated ISO codes;
        /// empty when none. Each code is also the flag file's name.
        /// </summary>
        public string Countries { get; }

        /// <summary>
        /// A picker-only icon the creator set on the variant component; empty means the vehicle's
        /// thumbnail, as vanilla sends it.
        /// </summary>
        public string Icon { get; }

        /// <summary>
        /// The collection this vehicle's family belongs to, by the top-most parent pack's asset
        /// GUID; empty when the family has no parent. Replaces the upload as the picker's folder.
        /// </summary>
        public string Collection { get; }

        /// <summary>The top-most parent pack's prefab name, the collection's heading.</summary>
        public string CollectionTitle { get; }

        /// <summary>The collection's icon: the top pack's Upload Icon, else its UI Object icon; empty when neither.</summary>
        public string CollectionIcon { get; }

        public VehicleStats(
            Entity entity, int maxSpeed, int passengers, string group, string groupTitle,
            string family, string familyTitle, string familyIcon, string groupIcon, int index,
            float acceleration, float braking, string energyType, int carriages, int length,
            string packageDate, string theme, string author, string countries, string icon,
            string collection = "", string collectionTitle = "", string collectionIcon = "") {
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
        }

        /// <inheritdoc/>
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
