import { ModRegistrar, ModuleRegistryExtend } from "cs2/modding";
import { initialize } from "vanilla/Components";
import { SelectVehiclesSection } from "./components/select-vehicles-section";
import { installCountryField } from "./editor/country-field";

/**
 * The selected-info panel does NOT render section components through their own module exports. It
 * looks them up in this map, keyed by the C# section type name, and the map captures each component
 * by reference when its module initializes.
 *
 * That is why extending "select-vehicles-section.tsx"'s own `SelectVehiclesSection` export does
 * nothing: the export binding changes, but the map still holds the original function, and the panel
 * only ever reads the map. Verified in game -- the extend applied cleanly and the panel kept
 * rendering vanilla rows.
 *
 * So the seam is the map itself.
 */
const SECTIONS_MODULE =
    "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx";
const SECTIONS_EXPORT = "selectedInfoSectionComponents";

/** The map key: the C# section type name, not the module path or the export name. */
const SELECT_VEHICLES_KEY = "Game.UI.InGame.SelectVehiclesSection";

const register: ModRegistrar = (moduleRegistry) => {
    // Resolves the shared set of vanilla components (InfoSection, InfoRow, Checkbox, ...) into
    // VC/VT/VF, plus the extras this mod needs.
    //
    // Dropdown/DropdownToggle are what the vanilla selector itself used: the popup floats over the
    // panel instead of pushing its contents down, and it brings the game's own focus handling.
    // The theme is game-dropdown.module.scss -- NOT common/input/dropdown/dropdown.module.scss,
    // which the shared base already registers under "dropdown" and is a different file.
    initialize(
        moduleRegistry,
        [
            { path: "game-ui/common/input/dropdown/dropdown.tsx", components: ["Dropdown"] },
            {
                path: "game-ui/common/input/dropdown/dropdown-toggle.tsx",
                components: ["DropdownToggle"],
            },
            // The game's icon button, for the pack page's close: the same widget every vanilla
            // panel header closes with, so it gets the game's hover and press treatment.
            { path: "game-ui/common/input/button/icon-button.tsx", components: ["IconButton"] },
        ],
        [
            { path: "game-ui/game/themes/game-dropdown.module.scss", name: "gameDropdown" },
            // The two themes vanilla's panel close uses: the round highlight on the button,
            // and the panel theme's closeButton placement.
            { path: "game-ui/common/input/button/themes/round-highlight-button.module.scss", name: "roundHighlightButton" },
            { path: "game-ui/common/panel/panel.module.scss", name: "panel" },
        ],
    );

    // The cast is needed because ModuleRegistryExtend is typed as
    // `(curr: ComponentType) => (props) => JSX.Element` -- it assumes you are wrapping a component.
    // At runtime extend is just a getter/setter swap over the named export, so it works on any
    // value, and the export we want is a plain object map. The typing is narrower than the API.
    const swapSection = ((sections: Record<string, unknown>) => ({
        ...sections,
        [SELECT_VEHICLES_KEY]: SelectVehiclesSection,
    })) as unknown as ModuleRegistryExtend;

    moduleRegistry.extend(SECTIONS_MODULE, SECTIONS_EXPORT, swapSection);

    // The editor's field for BTS_Variant.m_Countries, minus the select-all button. Same
    // trick as the section swap: the editor renders widgets from a map, and the map is the seam.
    installCountryField(moduleRegistry);

    // Registration is otherwise silent, and a silent no-op is indistinguishable from a bundle that
    // never loaded -- which cost a debugging pass. Every other mod in the log does the same.
    console.log("BetterTransitSelector UI module registered.");
};

export default register;
