import { ModRegistrar, ModuleRegistryExtend } from "cs2/modding";
import { initialize } from "vanilla/Components";
import { SelectVehiclesSection } from "./components/select-vehicles-section";
import { installCountryField } from "./editor/country-field";

const SECTIONS_MODULE =
    "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx";
const SECTIONS_EXPORT = "selectedInfoSectionComponents";

const SELECT_VEHICLES_KEY = "Game.UI.InGame.SelectVehiclesSection";

const register: ModRegistrar = (moduleRegistry) => {
    initialize(
        moduleRegistry,
        [
            { path: "game-ui/common/input/dropdown/dropdown.tsx", components: ["Dropdown"] },
            {
                path: "game-ui/common/input/dropdown/dropdown-toggle.tsx",
                components: ["DropdownToggle"],
            },

            { path: "game-ui/common/input/button/icon-button.tsx", components: ["IconButton"] },
        ],
        [
            { path: "game-ui/game/themes/game-dropdown.module.scss", name: "gameDropdown" },

            { path: "game-ui/common/input/button/themes/round-highlight-button.module.scss", name: "roundHighlightButton" },
            { path: "game-ui/common/panel/panel.module.scss", name: "panel" },
            // The colour reset button's look (its action-button), reused by the train builder's car card.
            {
                path: "game-ui/game/components/selected-info-panel/selected-info-sections/visual-customize-section/visual-customize-section.module.scss",
                name: "visualCustomize",
            },
        ],
    );

    const swapSection = ((sections: Record<string, unknown>) => ({
        ...sections,
        [SELECT_VEHICLES_KEY]: SelectVehiclesSection,
    })) as unknown as ModuleRegistryExtend;

    moduleRegistry.extend(SECTIONS_MODULE, SECTIONS_EXPORT, swapSection);

    installCountryField(moduleRegistry);

    console.log("BetterTransitSelector UI module registered.");
};

export default register;
