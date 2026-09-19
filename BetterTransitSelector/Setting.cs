using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using System.Collections.Generic;

namespace BetterTransitSelector
{
    /// <summary>
    /// Which stats the selector shows on each row.
    /// </summary>
    /// <remarks>
    /// Every one of these reads a field the vehicle prefab already carries, so none of them costs a
    /// lookup at display time — the whole table is built once per load either way. They are options
    /// because a row has limited width and what matters differs by player: a passenger-network
    /// builder wants capacity, someone timetabling wants acceleration.
    ///
    /// Speed and capacity default on because they are what the feedback asked for; the rest default
    /// off so the row does not start cluttered.
    /// </remarks>
    [FileLocation(nameof(BetterTransitSelector))]
    [SettingsUIGroupOrder(kDisplayGroup, kStatsGroup, kAdvancedGroup, kCreatorsGroup)]
    [SettingsUIShowGroupName(kDisplayGroup, kStatsGroup, kAdvancedGroup, kCreatorsGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";

        public const string kSortingGroup = "Sorting";

        public const string kDisplayGroup = "Display";

        public const string kStatsGroup = "Stats";

        public const string kAdvancedGroup = "Advanced";

        public const string kCreatorsGroup = "Creators";

        public Setting(IMod mod) : base(mod)
        {
        }

        /// <summary>
        /// How the lists are ordered.
        /// </summary>
        /// <remarks>
        /// Set from the filter panel's Sort row, not from the options screen -- hidden here so
        /// there is one place to change it -- but kept as a setting so the choice persists across
        /// sessions. The panel uses tool buttons, not a <c>&lt;select&gt;</c>, which takes the
        /// whole cohtml view down.
        /// </remarks>
        [SettingsUIHidden]
        public SortOrder Sorting { get; set; } = SortOrder.Default;

        public enum SortOrder
        {
            /// <summary>Whatever order the game emitted, which is what vanilla shows.</summary>
            Default,

            /// <summary>Alphabetical, by the name the player actually sees.</summary>
            Name,

            /// <summary>Most recently published upload first.</summary>
            Newest,

            /// <summary>Oldest upload first.</summary>
            Oldest,

            /// <summary>Fastest first. The stat you reach for when picking stock for a long line.</summary>
            Speed,

            /// <summary>Highest capacity first. Likewise, for a busy one.</summary>
            Passengers,

            /// <summary>Longest consist first.</summary>
            Length,
        }

        /// <summary>
        /// Minimum width of the open vehicle list, in UI units; 0 lets it fit its contents.
        /// </summary>
        /// <remarks>
        /// The same route Find It takes for its panel size: a slider here, the number pushed to
        /// the UI, applied as an inline style. It is a minimum rather than a width so a list whose
        /// rows are wider than the setting still fits them, and it is an absolute value because the
        /// list is a shrink-to-fit popup where a percentage would be circular.
        /// </remarks>
        [SettingsUISlider(min = 0, max = 1200, step = 20, unit = Unit.kInteger)]
        [SettingsUISection(kSection, kDisplayGroup)]
        public int ListWidth { get; set; }

        /// <summary>Height of the open vehicle list's scroll box, in UI units; 0 for the default.</summary>
        [SettingsUISlider(min = 0, max = 1600, step = 20, unit = Unit.kInteger)]
        [SettingsUISection(kSection, kDisplayGroup)]
        public int ListHeight { get; set; }

        /// <summary>Width of the favourites panel, in UI units; 0 for the default.</summary>
        [SettingsUISlider(min = 0, max = 1200, step = 20, unit = Unit.kInteger)]
        [SettingsUISection(kSection, kDisplayGroup)]
        public int FavouritesWidth { get; set; }

        /// <summary>Height of the favourites panel's scroll box, in UI units; 0 for the default.</summary>
        [SettingsUISlider(min = 0, max = 1600, step = 20, unit = Unit.kInteger)]
        [SettingsUISection(kSection, kDisplayGroup)]
        public int FavouritesHeight { get; set; }

        /// <summary>
        /// Which unit speeds are shown in.
        /// </summary>
        /// <remarks>
        /// An override rather than a derivation from the game's unit system, because the two do not
        /// always agree: a UK player runs the game in metric and still thinks of train speeds in
        /// mph. The game itself never displays a speed, so there is no vanilla convention to follow.
        /// </remarks>
        [SettingsUISection(kSection, kDisplayGroup)]
        public SpeedUnit Speed { get; set; } = SpeedUnit.Auto;

        /// <summary>
        /// Platform fit on train and subway lines: the warning marker on consists longer than
        /// the line's shortest platform, the platform length in tooltips and pages, and the
        /// "Fits" filter. Off, the platform is never measured and none of that appears.
        /// </summary>
        [SettingsUISection(kSection, kDisplayGroup)]
        public bool PlatformFit { get; set; } = true;

        /// <summary>
        /// Show an info button on pack headers that opens the upload's page (description,
        /// likes, links, screenshots) from the game's mod cache.
        /// </summary>
        /// <remarks>
        /// Behind the options screen's Advanced toggle and off by default: the pages are a
        /// reading feature beside a picking tool, and the buttons they add to every row and
        /// header are noise for a player who only wants to pick.
        /// </remarks>
        [SettingsUIAdvanced]
        [SettingsUISection(kSection, kAdvancedGroup)]
        public bool PackPages { get; set; }

        /// <summary>Show an info button on vehicle rows that opens the vehicle's page.</summary>
        [SettingsUIAdvanced]
        [SettingsUISection(kSection, kAdvancedGroup)]
        public bool VehiclePages { get; set; }

        /// <summary>
        /// Copies every pack the player made in the editor into the shared-prefabs folder, for
        /// handing to another creator. See <see cref="Domain.SharedPrefabs"/>.
        /// </summary>
        [SettingsUIAdvanced]
        [SettingsUIButton]
        [SettingsUISection(kSection, kCreatorsGroup)]
        public bool ExportPacks
        {
            set
            {
                var system = Unity.Entities.World.DefaultGameObjectInjectionWorld?
                    .GetExistingSystemManaged<Systems.BTS_VehicleStatsSystem>();
                var count = system?.ExportSharedPacks() ?? 0;
                Mod.Instance.ModLog.Info($"ExportPacks -- {count} pack(s) exported to {Domain.SharedPrefabs.Folder}");
            }
        }

        /// <summary>Opens the shared-prefabs folder in the file browser.</summary>
        [SettingsUIAdvanced]
        [SettingsUIButton]
        [SettingsUISection(kSection, kCreatorsGroup)]
        public bool OpenSharedFolder
        {
            set { Domain.SharedPrefabs.Open(); }
        }

        public enum SpeedUnit
        {
            /// <summary>km/h under the metric unit system, mph under the imperial one.</summary>
            Auto,

            Kph,

            Mph,
        }

        [SettingsUISection(kSection, kStatsGroup)]
        public bool ShowMaxSpeed { get; set; } = true;

        [SettingsUISection(kSection, kStatsGroup)]
        public bool ShowPassengers { get; set; } = true;

        [SettingsUISection(kSection, kStatsGroup)]
        public bool ShowAcceleration { get; set; }

        [SettingsUISection(kSection, kStatsGroup)]
        public bool ShowBraking { get; set; }

        [SettingsUISection(kSection, kStatsGroup)]
        public bool ShowEnergyType { get; set; }

        [SettingsUISection(kSection, kStatsGroup)]
        public bool ShowCarriages { get; set; }

        [SettingsUISection(kSection, kStatsGroup)]
        public bool ShowLength { get; set; }

        public override void SetDefaults()
        {
            Sorting = SortOrder.Default;
            ListWidth = 0;
            ListHeight = 0;
            FavouritesWidth = 0;
            FavouritesHeight = 0;
            Speed = SpeedUnit.Auto;
            PlatformFit = false;
            PackPages = false;
            VehiclePages = false;
            ShowMaxSpeed = true;
            ShowPassengers = true;
            ShowAcceleration = false;
            ShowBraking = false;
            ShowEnergyType = false;
            ShowCarriages = false;
            ShowLength = false;
        }
    }
}
