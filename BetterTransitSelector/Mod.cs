namespace BetterTransitSelector {
    #region Using Statements

    using System.Linq;

    using Colossal;

    using Game;
    using Game.Modding;
    using Game.SceneFlow;

    using BetterTransitSelector.Prefabs;
    using BetterTransitSelector.Systems;

    using ModsCommon.Extensions;
    using ModsCommon.Mod;

    #endregion
    
    public sealed class Mod : ModsCommonBase<Mod>, IMod {
        
        
        public override string ModName => "Better Transit Selector";

        
        public override string Id => "BetterTransitSelector";

      
        protected override string UiHostPrefix => "bettertransitselector";

       
        protected override ModSetting CreateSettings(IMod mod) => new Setting(mod);

       
        protected override IDictionarySource CreateEnUsLocalization(ModSetting settings) =>
            new LocaleHelper("BetterTransitSelector.Locale.json")
                .GetAvailableLanguages()
                .First(s => s.LocaleId == "en-US");

        protected override void OnAfterLoad(UpdateSystem updateSystem) {
            CountryFieldBuilders.Register();

            foreach (var item in new LocaleHelper("BetterTransitSelector.Locale.json").GetAvailableLanguages()) {
                if (item.LocaleId != "en-US") {
                    GameManager.instance.localizationManager.AddSource(item.LocaleId, item);
                }
            }
        }

        /// <inheritdoc/>
        protected override void RegisterSystems(UpdateSystem updateSystem) {
            updateSystem.UpdateAt<BTS_VehicleStatsSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<BTS_LineSummarySystem>(SystemUpdatePhase.UIUpdate);
        }
    }
}
