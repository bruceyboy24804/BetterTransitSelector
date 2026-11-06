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
            // First, so the BTS components are known/recovered before the systems that read them exist.
            updateSystem.UpdateAt<BTS_PrefabComponentSystem>(SystemUpdatePhase.PrefabUpdate);

            // Custom cars are prefabs, so they must exist before a save is read; PrefabUpdate runs from the main
            // menu on, and the system also recreates them in OnGamePreload.
            updateSystem.UpdateAt<BTS_CustomCarSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAt<BTS_VehicleStatsSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<BTS_LineSummarySystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<BTS_ConsistBuilderSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<BTS_ConsistPreviewSystem>(SystemUpdatePhase.UIUpdate);

            // Between the depot's command buffer playback and vanilla's InitializeSystem, which places the
            // cars from the layout: the only window where a new train can be rebuilt as if vanilla built it.
            updateSystem.UpdateBefore<BTS_ConsistSystem, Game.Vehicles.InitializeSystem>(SystemUpdatePhase.Modification5);

            // Straight after TransportLineSystem, whose CancelAbandon would otherwise clear a refit's retire flag.
            updateSystem.UpdateAfter<BTS_RefitSystem, Game.Simulation.TransportLineSystem>(SystemUpdatePhase.GameSimulation);

            // Before the train AI, whose StopBoarding swaps cargo cars from the loading plan this system clears.
            updateSystem.UpdateBefore<BTS_CargoCarSystem, Game.Simulation.TransportTrainAISystem>(SystemUpdatePhase.GameSimulation);
        }
    }
}
