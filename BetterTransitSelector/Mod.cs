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

    /// <summary>
    /// Mod entry point. Lifecycle (logging, settings, i18n, Harmony, asset hosting) is handled by
    /// <see cref="ModsCommonBase{TSelf}"/>; this class only supplies the mod-specific pieces.
    /// </summary>
    // The base deliberately does not implement IMod — the game instantiates every IMod-derived type
    // in the assembly, and shared code is compiled in by source inclusion, so an abstract IMod base
    // would be picked up and crash. The concrete class declares it instead.
    public sealed class Mod : ModsCommonBase<Mod>, IMod {
        
        //unlisted mod id > 158942
        /// <inheritdoc/>
        public override string ModName => "Better Transit Selector";

        /// <inheritdoc/>
        // Binding group for every C# <-> TypeScript binding. MUST match "id" in UI/mod.json.
        public override string Id => "BetterTransitSelector";

        /// <inheritdoc/>
        protected override string UiHostPrefix => "bettertransitselector";

        /// <inheritdoc/>
        protected override ModSetting CreateSettings(IMod mod) => new Setting(mod);

        /// <inheritdoc/>
        /// <remarks>
        /// Every string, settings and UI alike, lives in the embedded <c>Locale.json</c> and its
        /// <c>Locale/&lt;locale&gt;.json</c> translations (Find It's and Node Controller's layout).
        /// The base wants an en-US source from this method, so it gets the English dictionary;
        /// the other languages are registered in <see cref="OnAfterLoad"/>.
        ///
        /// The helper is built here and not in a field initializer: the game instantiates mods
        /// with <c>FormatterServices.GetUninitializedObject</c> (ModManager.cs:121), which runs no
        /// constructor, so a field initializer never executes -- that was a NullReferenceException
        /// that took the whole mod down at load. Find It and Node Controller build it inline in
        /// OnLoad for the same reason.
        /// </remarks>
        protected override IDictionarySource CreateEnUsLocalization(ModSetting settings) =>
            new LocaleHelper("BetterTransitSelector.Locale.json")
                .GetAvailableLanguages()
                .First(s => s.LocaleId == "en-US");

        /// <inheritdoc/>
        protected override void OnAfterLoad(UpdateSystem updateSystem) {
            // The editor field for Country: the game's flags dropdown with a five-country cap.
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
