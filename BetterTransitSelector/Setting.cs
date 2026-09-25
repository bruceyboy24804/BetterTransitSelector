using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using System.Collections.Generic;

namespace BetterTransitSelector
{
   
    [FileLocation(nameof(BetterTransitSelector))]
    [SettingsUIGroupOrder(kDisplayGroup, kStatsGroup, kPagesGroup, kCreatorsGroup)]
    [SettingsUIShowGroupName(kDisplayGroup, kStatsGroup, kPagesGroup, kCreatorsGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";

        public const string kSortingGroup = "Sorting";

        public const string kDisplayGroup = "Display";

        public const string kStatsGroup = "Stats";

        public const string kPagesGroup = "Pages";

        public const string kCreatorsGroup = "Creators";

        public Setting(IMod mod) : base(mod)
        {
        }

       
        [SettingsUIHidden]
        public SortOrder Sorting { get; set; } = SortOrder.Default;

        public enum SortOrder
        {
            Default,

            Name,

            Newest,

            Oldest,

            Speed,

            Passengers,

            Length,
        }

        
        [SettingsUISlider(min = 0, max = 1200, step = 20, unit = Unit.kInteger)]
        [SettingsUISection(kSection, kDisplayGroup)]
        public int ListWidth { get; set; }

        [SettingsUISlider(min = 0, max = 1600, step = 20, unit = Unit.kInteger)]
        [SettingsUISection(kSection, kDisplayGroup)]
        public int ListHeight { get; set; }

        [SettingsUISlider(min = 0, max = 1200, step = 20, unit = Unit.kInteger)]
        [SettingsUISection(kSection, kDisplayGroup)]
        public int FavouritesWidth { get; set; }

        [SettingsUISlider(min = 0, max = 1600, step = 20, unit = Unit.kInteger)]
        [SettingsUISection(kSection, kDisplayGroup)]
        public int FavouritesHeight { get; set; }

        
        [SettingsUISection(kSection, kDisplayGroup)]
        public SpeedUnit Speed { get; set; } = SpeedUnit.Auto;

        
        [SettingsUIHidden]
        public bool RecentOpen { get; set; } = true;

        
        [SettingsUISection(kSection, kPagesGroup)]
        public bool PackPages { get; set; }

        [SettingsUISection(kSection, kPagesGroup)]
        public bool VehiclePages { get; set; }

        
        [SettingsUIAdvanced]
        [SettingsUIButton]
        [SettingsUISection(kSection, kCreatorsGroup)]
        public bool ExportPacks
        {
            set
            {
                var system = Unity.Entities.World.DefaultGameObjectInjectionWorld?
                    .GetExistingSystemManaged<Systems.BTS_VehicleStatsSystem>();
                system?.ExportSharedPacks();
            }
        }

        [SettingsUIAdvanced]
        [SettingsUIButton]
        [SettingsUISection(kSection, kCreatorsGroup)]
        public bool OpenSharedFolder
        {
            set { Domain.SharedPrefabs.Open(); }
        }

        public enum SpeedUnit
        {
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
            PackPages = true;
            VehiclePages = true;
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
