import { useCallback, useMemo } from "react";
import type { ModuleRegistry, ModuleRegistryExtend } from "cs2/modding";

/**
 * The editor field for BTS_Variant.m_Countries: the game's own FlagsField, copied line for
 * line from the bundle (game-ui/editor/widgets/fields/enum-field.tsx, `FlagsField`), with one
 * thing removed -- the "Select Nothing/Everything" button at the top of its menu.
 *
 * Everything else is vanilla's: the same FocusableEditorItem wrapper, Tooltip, editor-item row
 * classes, Dropdown with the editor-dropdown theme, DropdownFlagItem rows and DropdownToggle
 * with the same sounds. Nothing here is styled by this mod, so it looks identical to every
 * other flags field in the editor -- which the first attempt, built from generic widgets, did
 * not.
 *
 * The editor resolves widgets through the exported `editorWidgetComponents` map, read at render
 * time, so the FlagsField entry is wrapped: this component for the one widget path that is
 * ours, the original for every other flags field.
 */

interface EnumMemberWire {
    displayName: unknown;
    value: [number, number];
    disabled?: boolean;
}

interface WidgetRenderProps {
    parent: { group: string; path: string };
    path: string;
    props: {
        displayName?: unknown;
        value: [number, number];
        enumMembers: EnumMemberWire[];
        disabled?: boolean;
        tooltip?: unknown;
    };
}

const FLAGS_FIELD_KEY = "Game.UI.Widgets.FlagsField";
const OUR_PATH = "m_Countries";

// Vanilla's pieces, resolved from the bundle in installCountryField. Names are the bundle's
// export names; the module paths are where the bundle registers them.
let V: {
    FocusableEditorItem: React.ComponentType<{ disabled?: boolean; children?: React.ReactNode }>;
    Tooltip: React.ComponentType<{ tooltip?: React.ReactNode; children?: React.ReactNode }>;
    /** Vanilla's `Localized` (Tu): renders any LocElement, including the { __Type, id, value } objects C# widgets send. */
    LocalizedString: React.ComponentType<{ value: unknown }>;
    /** Vanilla's `renderLocalized` (yu): the same, to a string. */
    renderLocalized: (loc: ReturnType<typeof V.useLocalization>, value: unknown) => string;
    Dropdown: React.ComponentType<{ theme?: unknown; initialFocused?: unknown; content: React.ReactNode; children?: React.ReactNode }>;
    DropdownToggle: React.ComponentType<{ sounds?: unknown; className?: string; disabled?: boolean; children?: React.ReactNode }>;
    DropdownFlagItem: React.ComponentType<{
        theme?: unknown;
        focusKey?: unknown;
        value: bigint;
        checked: boolean;
        onChange: (value: bigint, checked: boolean) => void;
        children?: React.ReactNode;
    }>;
    useLocalization: () => { translate: (id: string, fallback?: string | null) => string | null };
    useWidgetId: (parent: WidgetRenderProps["parent"], path: string) => { group: string; path: string };
    setValue: (id: { group: string; path: string }, value: [number, number]) => void;
    longToBigInt: (v: [number, number]) => bigint;
    bigIntToLong: (v: bigint) => [number, number];
    fieldStyles: Record<string, string>;
    dropdownTheme: Record<string, string>;
    defaultButtonSounds: Record<string, unknown>;
};

/** Vanilla's `jz`: a member's display name resolved to text, or " " when none matches. */
const memberText = (loc: ReturnType<typeof V.useLocalization>, members: { displayName: unknown; value: bigint }[], value: bigint) => {
    const m = members.find((e) => e.value === value);
    return m ? V.renderLocalized(loc, m.displayName) : " ";
};

/** Vanilla's `FlagsField` (Iz), minus the toggle-all button. */
const FlagsFieldNoToggle = ({
    label,
    value,
    enumMembers,
    disabled,
    tooltip,
    onChange,
}: {
    label: React.ReactNode;
    value: bigint;
    enumMembers: { displayName: unknown; value: bigint }[];
    disabled?: boolean;
    tooltip?: unknown;
    onChange: (v: bigint) => void;
}) => {
    const loc = V.useLocalization();
    const summary = useMemo(() => {
        if (value !== 0n) {
            const exact = enumMembers.map((e) => e.value).find((e) => e === value);
            return exact !== undefined
                ? memberText(loc, enumMembers, exact)
                : enumMembers
                      .filter((e) => e.value !== 0n && (value & e.value) === e.value)
                      .map((e) => V.renderLocalized(loc, e.displayName))
                      .join(", ") || " ";
        }
        return memberText(loc, enumMembers, 0n);
    }, [loc, enumMembers, value]);
    const toggle = useCallback((bits: bigint, on: boolean) => onChange(on ? value | bits : value & ~bits), [onChange, value]);
    const sounds = useMemo(() => ({ ...V.defaultButtonSounds, hover: null, focus: null }), []);

    return (
        <V.FocusableEditorItem disabled={disabled}>
            <V.Tooltip tooltip={tooltip ? <V.LocalizedString value={tooltip} /> : undefined}>
                <div className={V.fieldStyles.row}>
                    <div className={V.fieldStyles.label}>{label}</div>
                    <div className={V.fieldStyles.control}>
                        <V.Dropdown
                            theme={V.dropdownTheme}
                            initialFocused={0}
                            content={
                                <>
                                    {/* Vanilla renders its Select Nothing/Everything Button here. Removed. */}
                                    {enumMembers.map((e, n) => (
                                        <V.DropdownFlagItem
                                            key={n}
                                            theme={V.dropdownTheme}
                                            focusKey={n}
                                            value={e.value}
                                            checked={(e.value & value) === e.value}
                                            onChange={toggle}
                                        >
                                            <V.LocalizedString value={e.displayName} />
                                        </V.DropdownFlagItem>
                                    ))}
                                </>
                            }
                        >
                            <V.DropdownToggle sounds={sounds} className={V.fieldStyles.dropdownToggle} disabled={disabled}>
                                {summary}
                            </V.DropdownToggle>
                        </V.Dropdown>
                    </div>
                </div>
            </V.Tooltip>
        </V.FocusableEditorItem>
    );
};

/** Vanilla's `BoundFlagsField` (yz): binds the widget props to the field above. */
const BoundCountryField = ({ parent, path, props }: WidgetRenderProps) => {
    const id = V.useWidgetId(parent, path);
    const members = useMemo(
        () => props.enumMembers.map((m) => ({ displayName: m.displayName, value: V.longToBigInt(m.value), disabled: m.disabled })),
        [props.enumMembers],
    );
    const onChange = useCallback((v: bigint) => V.setValue(id, V.bigIntToLong(v)), [id]);

    return (
        <FlagsFieldNoToggle
            label={<V.LocalizedString value={props.displayName} />}
            value={V.longToBigInt(props.value)}
            enumMembers={members}
            disabled={props.disabled}
            tooltip={props.tooltip}
            onChange={onChange}
        />
    );
};

export const installCountryField = (registry: ModuleRegistry) => {
    const get = (path: string) => registry.registry.get(path);
    const editorItem = get("game-ui/editor/widgets/item/editor-item.tsx");
    const tooltip = get("game-ui/common/tooltip/tooltip.tsx");
    const locStr = get("game-ui/common/localization/localized.tsx");
    const loc = get("game-ui/common/localization/localization.tsx");
    const dropdown = get("game-ui/common/input/dropdown/dropdown.tsx");
    const dropdownToggle = get("game-ui/common/input/dropdown/dropdown-toggle.tsx");
    const flagItem = get("game-ui/common/input/dropdown/items/dropdown-flag-item.tsx");
    const button = get("game-ui/common/input/button/button.tsx");
    const widgetProps = get("game-ui/widgets/components/widget-props.ts");
    const widgetBindings = get("game-ui/widgets/data-binding/widget-bindings.ts");
    const math = get("game-ui/common/math.ts");
    const fieldStyles = get("game-ui/editor/widgets/item/editor-item.module.scss");
    const dropdownTheme = get("game-ui/editor/themes/editor-dropdown.module.scss");

    const missing = [
        editorItem, tooltip, locStr, loc, dropdown, dropdownToggle, flagItem, button,
        widgetProps, widgetBindings, math, fieldStyles, dropdownTheme,
    ].some((m) => !m);
    if (missing) {
        console.warn("[BetterTransitSelector] editor modules not found; the Country field keeps the vanilla widget");
        return;
    }

    V = {
        FocusableEditorItem: editorItem!.FocusableEditorItem,
        Tooltip: tooltip!.Tooltip,
        LocalizedString: locStr!.Localized,
        renderLocalized: locStr!.renderLocalized,
        Dropdown: dropdown!.Dropdown,
        DropdownToggle: dropdownToggle!.DropdownToggle,
        DropdownFlagItem: flagItem!.DropdownFlagItem,
        useLocalization: loc!.useCachedLocalization,
        useWidgetId: widgetProps!.useWidgetId,
        setValue: widgetBindings!.setValue,
        longToBigInt: math!.longToBigInt,
        bigIntToLong: math!.bigIntToLong,
        fieldStyles: fieldStyles!.classes,
        dropdownTheme: dropdownTheme!.classes,
        defaultButtonSounds: button!.defaultButtonSounds,
    };

    // Cast for the same reason as the section swap in index.tsx: ModuleRegistryExtend is typed
    // for wrapping a component, but extend is a plain getter/setter swap and this export is a map.
    const swap = ((components: Record<string, React.ComponentType<WidgetRenderProps>>) => {
        const Original = components[FLAGS_FIELD_KEY];
        if (!Original) return components;
        const Routed = (p: WidgetRenderProps) =>
            p.path === OUR_PATH || p.path?.endsWith("." + OUR_PATH) ? <BoundCountryField {...p} /> : <Original {...p} />;
        return { ...components, [FLAGS_FIELD_KEY]: Routed };
    }) as unknown as ModuleRegistryExtend;

    registry.extend("game-ui/editor/widgets/editor-widget-renderer.tsx", "editorWidgetComponents", swap);
};
