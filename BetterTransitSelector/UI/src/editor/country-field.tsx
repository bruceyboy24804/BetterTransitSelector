import { useCallback, useMemo } from "react";
import type { ModuleRegistry, ModuleRegistryExtend } from "cs2/modding";

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

let V: {
    FocusableEditorItem: React.ComponentType<{ disabled?: boolean; children?: React.ReactNode }>;
    Tooltip: React.ComponentType<{ tooltip?: React.ReactNode; children?: React.ReactNode }>;

    LocalizedString: React.ComponentType<{ value: unknown }>;

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

const memberText = (loc: ReturnType<typeof V.useLocalization>, members: { displayName: unknown; value: bigint }[], value: bigint) => {
    const m = members.find((e) => e.value === value);
    return m ? V.renderLocalized(loc, m.displayName) : " ";
};

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

    const swap = ((components: Record<string, React.ComponentType<WidgetRenderProps>>) => {
        const Original = components[FLAGS_FIELD_KEY];
        if (!Original) return components;

        const isOurs = (path: unknown) =>
            typeof path === "string" && (path === OUR_PATH || path.endsWith("." + OUR_PATH));
        const Routed = (p: WidgetRenderProps) =>
            isOurs(p.path) ? <BoundCountryField {...p} /> : <Original {...p} />;
        return { ...components, [FLAGS_FIELD_KEY]: Routed };
    }) as unknown as ModuleRegistryExtend;

    registry.extend("game-ui/editor/widgets/editor-widget-renderer.tsx", "editorWidgetComponents", swap);
};
