namespace BetterTransitSelector.Domain {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal.UI.Binding;

    using Game.Prefabs;

    using Unity.Entities;

    #endregion

    /// <summary>What the train builder shows for the selected line: its built train and the cars it can use.</summary>
    public sealed class ConsistBuilder : IJsonWritable {
        /// <summary>Nothing selected, or the selected line is not a train, tram or subway line.</summary>
        public static readonly ConsistBuilder Empty = new ConsistBuilder();

        /// <summary>True when a rail line is selected.</summary>
        public bool Valid;

        /// <summary>The line's display name, as the game renders it on labels.</summary>
        public string LineName = string.Empty;

        /// <summary>
        /// One of <c>Vanilla</c>, <c>Ok</c>, <c>NoLead</c>, <c>NeedsPrimary</c>, <c>NeedsSecondary</c>,
        /// <c>NoEngine</c>. The UI turns it into text and, for the two <c>Needs*</c> codes, a fix button.
        /// </summary>
        public string Status = "Vanilla";

        /// <summary>Running trains that differ from the built train and are not already leaving the line.</summary>
        public int Mismatched;

        /// <summary>A rolling refit is replacing them one at a time.</summary>
        public bool Refitting;

        /// <summary>The car to select as a primary model to fix <c>NeedsPrimary</c>.</summary>
        public Entity FixPrimary;

        /// <summary>The car to select as a secondary model to fix <c>NeedsSecondary</c>.</summary>
        public Entity FixSecondary;

        /// <summary>The built train, front first.</summary>
        public readonly List<ConsistSlot> Cars = new List<ConsistSlot>();

        /// <summary>Every car that can run on the line.</summary>
        public IReadOnlyList<ConsistCatalogueCar> Catalogue = new List<ConsistCatalogueCar>();

        /// <summary>
        /// Cars that belong with this line: its selected models, the cars those models are built from
        /// (<c>VehicleCarriageElement</c>), and anything already in the built train. The catalogue's default view.
        /// </summary>
        public readonly List<Entity> Related = new List<Entity>();

        /// <summary>The line's selected models, primary and secondary, each once: what "Start from" offers.</summary>
        public readonly List<Entity> Models = new List<Entity>();

        public void Write(IJsonWriter writer) {
            writer.TypeBegin("BetterTransitSelector.ConsistBuilder");
            writer.PropertyName("valid");        writer.Write(Valid);
            writer.PropertyName("lineName");     writer.Write(LineName);
            writer.PropertyName("status");       writer.Write(Status);
            writer.PropertyName("mismatched");   writer.Write(Mismatched);
            writer.PropertyName("refitting");    writer.Write(Refitting);
            writer.PropertyName("fixPrimary");   writer.Write(FixPrimary);
            writer.PropertyName("fixSecondary"); writer.Write(FixSecondary);

            writer.PropertyName("cars");
            writer.ArrayBegin(Cars.Count);
            foreach (var car in Cars) {
                car.Write(writer);
            }
            writer.ArrayEnd();

            writer.PropertyName("related");
            writer.ArrayBegin(Related.Count);
            foreach (var entity in Related) {
                writer.Write(entity);
            }
            writer.ArrayEnd();

            writer.PropertyName("models");
            writer.ArrayBegin(Models.Count);
            foreach (var entity in Models) {
                writer.Write(entity);
            }
            writer.ArrayEnd();

            writer.PropertyName("catalogue");
            writer.ArrayBegin(Catalogue.Count);
            foreach (var car in Catalogue) {
                car.Write(writer);
            }
            writer.ArrayEnd();

            writer.TypeEnd();
        }
    }

    /// <summary>One car of the built train.</summary>
    /// <summary>
    /// A car prefab's figures as the simulation reads them: capacity from its transport data, and top speed
    /// (sent in km/h, the unit the UI edits in), acceleration and braking (m/s²) from its <see cref="TrainData"/>.
    /// </summary>
    public readonly struct CarFigures {
        public int   Passengers   { get; }
        public int   Cargo        { get; }
        public float MaxSpeed     { get; }
        public float Acceleration { get; }
        public float Braking      { get; }

        public CarFigures(int passengers, int cargo, TrainData train) {
            Passengers   = passengers;
            Cargo        = cargo;
            MaxSpeed     = train.m_MaxSpeed * 3.6f;
            Acceleration = train.m_Acceleration;
            Braking      = train.m_Braking;
        }

        public void Write(IJsonWriter writer) {
            writer.TypeBegin("BetterTransitSelector.CarFigures");
            writer.PropertyName("passengers");   writer.Write(Passengers);
            writer.PropertyName("cargo");        writer.Write(Cargo);
            writer.PropertyName("maxSpeed");     writer.Write(MaxSpeed);
            writer.PropertyName("acceleration"); writer.Write(Acceleration);
            writer.PropertyName("braking");      writer.Write(Braking);
            writer.TypeEnd();
        }
    }

    /// <summary>
    /// One car of the built train, with everything the UI draws for it. Read fresh on every publish, not from
    /// the catalogue, because a custom car's prefab data only fills in the frame after it is created.
    /// </summary>
    public readonly struct ConsistSlot {
        public Entity Prefab   { get; }
        public string Id       { get; }
        public bool   Reversed { get; }

        /// <summary>The asset is no longer installed; the slot is skipped when trains are built.</summary>
        public bool Missing { get; }

        /// <summary>This car becomes the train's controller, and so must be a selected primary model.</summary>
        public bool Lead { get; }

        /// <summary>For a custom car, its original asset's prefab name; empty for an asset's own car.</summary>
        public string CustomOf { get; }

        public string Kind      { get; }
        public float  Length    { get; }
        public string Thumbnail { get; }

        /// <summary>The figures this car runs with.</summary>
        public CarFigures Figures { get; }

        /// <summary>The original asset's figures: equal to <see cref="Figures"/> unless this is a custom car.</summary>
        public CarFigures BaseFigures { get; }

        public ConsistSlot(Entity prefab, string id, bool reversed, bool missing, bool lead, string customOf,
                           string kind, float length, string thumbnail, CarFigures figures, CarFigures baseFigures) {
            Prefab      = prefab;
            Id          = id;
            Reversed    = reversed;
            Missing     = missing;
            Lead        = lead;
            CustomOf    = customOf;
            Kind        = kind;
            Length      = length;
            Thumbnail   = thumbnail;
            Figures     = figures;
            BaseFigures = baseFigures;
        }

        public void Write(IJsonWriter writer) {
            writer.TypeBegin("BetterTransitSelector.ConsistSlot");
            writer.PropertyName("prefab");      writer.Write(Prefab);
            writer.PropertyName("id");          writer.Write(Id);
            writer.PropertyName("reversed");    writer.Write(Reversed);
            writer.PropertyName("missing");     writer.Write(Missing);
            writer.PropertyName("lead");        writer.Write(Lead);
            writer.PropertyName("customOf");    writer.Write(CustomOf);
            writer.PropertyName("kind");        writer.Write(Kind);
            writer.PropertyName("length");      writer.Write(Length);
            writer.PropertyName("thumbnail");   writer.Write(Thumbnail);
            writer.PropertyName("figures");     Figures.Write(writer);
            writer.PropertyName("baseFigures"); BaseFigures.Write(writer);
            writer.TypeEnd();
        }
    }

    /// <summary>A car the player can add to the line's train. Custom cars are not listed; they are made in place.</summary>
    public readonly struct ConsistCatalogueCar {
        public Entity Entity    { get; }
        public string Id        { get; }

        /// <summary><c>Engine</c>, <c>Unit</c> (multiple-unit car), <c>Carriage</c> or <c>Other</c>.</summary>
        public string Kind      { get; }

        /// <summary>Car length in metres.</summary>
        public float  Length    { get; }
        public string Thumbnail { get; }

        /// <summary>Published title of the upload the car came in; empty for vanilla cars.</summary>
        public string Pack      { get; }

        /// <summary>Upload author; empty for vanilla cars.</summary>
        public string Author    { get; }

        public CarFigures Figures { get; }

        public ConsistCatalogueCar(Entity entity, string id, string kind, float length, string thumbnail,
                                   string pack, string author, CarFigures figures) {
            Entity    = entity;
            Id        = id;
            Kind      = kind;
            Length    = length;
            Thumbnail = thumbnail;
            Pack      = pack;
            Author    = author;
            Figures   = figures;
        }

        public void Write(IJsonWriter writer) {
            writer.TypeBegin("BetterTransitSelector.ConsistCatalogueCar");
            writer.PropertyName("entity");    writer.Write(Entity);
            writer.PropertyName("id");        writer.Write(Id);
            writer.PropertyName("kind");      writer.Write(Kind);
            writer.PropertyName("length");    writer.Write(Length);
            writer.PropertyName("thumbnail"); writer.Write(Thumbnail);
            writer.PropertyName("pack");      writer.Write(Pack);
            writer.PropertyName("author");    writer.Write(Author);
            writer.PropertyName("figures");   Figures.Write(writer);
            writer.TypeEnd();
        }
    }
}
