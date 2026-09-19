import { useState } from "react";
import { VC, VT } from "vanilla/Components";
import { c } from "utils/classes";
import {
    NO_FILTERS,
    filtersActive,
    flagUrl,
    useCountryName,
    useLineSummary,
    useStatOptions,
    useT,
    type VehicleFilters,
} from "../vehicle-stats";
import { setSorting } from "../bindings";
import styles from "./vehicle-filters.module.scss";

/** Setting.SortOrder names with their labels, in the order the row shows them. */
const SORT_ORDERS: { value: string; label: string }[] = [
    { value: "Default", label: "Game order" },
    { value: "Name", label: "Name" },
    { value: "Newest", label: "Newest" },
    { value: "Oldest", label: "Oldest" },
    { value: "Speed", label: "Fastest" },
    { value: "Passengers", label: "Passengers" },
    { value: "Length", label: "Longest" },
];

const STEP_DOWN = "Media/Glyphs/ThickStrokeArrowDown.svg";
const STEP_UP = "Media/Glyphs/ThickStrokeArrowUp.svg";

/** How far one stepper click moves each minimum. Coarse on purpose: these are thresholds, not values. */
const PASSENGER_STEP = 50;
const SPEED_STEP = 20;

export interface VehicleFiltersBarProps {
    filters: VehicleFilters;
    onChange: (filters: VehicleFilters) => void;
    /** Values actually present in this list, so no option is offered that would match nothing. */
    themes: string[];
    energies: string[];
    authors: string[];
    sources: string[];
    countries: string[];
}

/** Adds or removes a value, since every option here is a toggle rather than a radio. */
const toggle = (values: string[], value: string): string[] =>
    values.includes(value) ? values.filter((v) => v !== value) : [...values, value];

/**
 * The filter panel, laid out the way Find It lays out its options: label left, controls right.
 *
 * It reuses the game's own widgets throughout -- ToolButton with the tool-button theme for every
 * option, and the mouse-tool-options theme's start/end buttons for the steppers -- so the panel
 * reads as part of the game rather than as a web form dropped into it. Find It proved every one
 * of these pieces works inside a game panel, which is worth more than any styling of our own.
 *
 * Options are built from the values present in the current list, so a bus line offers no train
 * themes and an all-electric roster no diesel button. Offering a filter that can only empty the
 * list is worse than offering none.
 */
export const VehicleFiltersBar = ({
    filters,
    onChange,
    themes,
    energies,
    authors,
    sources,
    countries,
}: VehicleFiltersBarProps) => {
    const { sorting } = useStatOptions();
    const [sortOpen, setSortOpen] = useState(false);
    const t = useT();
    const countryName = useCountryName();
    const line = useLineSummary();
    const sourceLabel = (v: string) => t(`Source.${v}`, v);

    /** A row of text tool buttons, each a toggle. Hidden when there is nothing to choose between. */
    const buttonRow = (
        label: string,
        values: string[],
        selected: string[],
        key: "themes" | "energies" | "sources",
        // Optional display name per value: Source's values are our own keys and get translated;
        // themes and power types are the game's strings and show as they are.
        valueLabel: (value: string) => string = (v) => v,
    ) =>
        values.length > 1 && (
            <div className={styles.optionRow}>
                <div className={styles.optionLabel}>{label}</div>
                <div className={styles.optionContent}>
                    {values.map((value) => {
                        const on = selected.includes(value);
                        return (
                            <VC.ToolButton
                                key={value}
                                src=""
                                selected={on}
                                multiSelect={true}
                                className={c(
                                    VT.toolButton.button,
                                    styles.textButton,
                                    on ? styles.selected : "",
                                )}
                                onSelect={() => onChange({ ...filters, [key]: toggle(selected, value) })}
                            >
                                {valueLabel(value)}
                            </VC.ToolButton>
                        );
                    })}
                </div>
            </div>
        );

    /**
     * The sort order as a dropdown. Lives here rather than in the options screen because it is
     * changed while looking at the list, and persists via the settings on the C# side.
     *
     * NOT vanilla's Dropdown: that one portals its menu to the document root, outside this
     * panel, and the panel keeps the vehicle list open only by swallowing mousedown inside its
     * own subtree -- so picking a sort option through a portalled menu would shut the list.
     * The toggle is vanilla's widget with the game dropdown theme, and the menu is the same
     * theme's classes on a div that opens inline, inside the panel, where the swallow holds.
     */
    const current = SORT_ORDERS.find((o) => o.value === sorting) ?? SORT_ORDERS[0];
    const sortRow = (
        <div className={styles.optionRow}>
            <div className={styles.optionLabel}>{t("Sort", "Sort")}</div>
            <div className={c(styles.optionContent, styles.sortContent)}>
                <VC.DropdownToggle
                    theme={VT.gameDropdown}
                    className={styles.sortToggle}
                    onClick={() => setSortOpen(!sortOpen)}
                >
                    {t(`Sort.${current.value}`, current.label)}
                </VC.DropdownToggle>
                {sortOpen && (
                    <div className={c(VT.gameDropdown.dropdownMenu, styles.sortMenu)}>
                        {SORT_ORDERS.map(({ value, label }) => (
                            <div
                                key={value}
                                className={c(
                                    VT.gameDropdown.dropdownItem,
                                    styles.sortItem,
                                    value === sorting ? styles.sortItemOn : "",
                                )}
                                onClick={() => {
                                    setSorting(value);
                                    setSortOpen(false);
                                }}
                            >
                                {t(`Sort.${value}`, label)}
                            </div>
                        ))}
                    </div>
                )}
            </div>
        </div>
    );

    /**
     * A stepper: [v] value [^]. The value is display-only, as in Find It; the steps are coarse
     * because a minimum is a threshold, and "at least 300" is the question, never "at least 317".
     */
    const stepper = (label: string, value: number, step: number, set: (v: number) => void) => (
        <div className={styles.optionRow}>
            <div className={styles.optionLabel}>{label}</div>
            <div className={styles.optionContent}>
                <VC.ToolButton
                    src={STEP_DOWN}
                    disabled={value <= 0}
                    className={c(
                        VT.toolButton.button,
                        VT.mouseToolOptions.startButton,
                        styles.stepButton,
                    )}
                    onSelect={() => set(Math.max(0, value - step))}
                />
                <div className={c(VT.mouseToolOptions.numberField, styles.numberField)}>
                    {value > 0 ? String(value) : t("Any", "Any")}
                </div>
                <VC.ToolButton
                    src={STEP_UP}
                    className={c(
                        VT.toolButton.button,
                        VT.mouseToolOptions.endButton,
                        styles.stepButton,
                    )}
                    onSelect={() => set(value + step)}
                />
            </div>
        </div>
    );

    return (
        <div className={styles.panel}>
            {sortRow}
            {/* Source: vanilla stock versus subscribed. Both values are always offered when the
                line has both, so "hide the vanilla ones" is one click on Modded. */}
            {buttonRow(t("Source", "Source"), sources, filters.sources, "sources", sourceLabel)}

            {/* Countries as flag buttons, the way the creators asked countries be shown. Only
                the countries some vehicle on this line declares, like every other row. */}
            {countries.length > 1 && (
                <div className={styles.optionRow}>
                    <div className={styles.optionLabel}>{t("Countries", "Countries")}</div>
                    <div className={styles.optionContent}>
                        {countries.map((iso) => {
                            const on = filters.countries.includes(iso);
                            return (
                                <VC.ToolButton
                                    key={iso}
                                    src={flagUrl(iso)}
                                    selected={on}
                                    multiSelect={true}
                                    tooltip={countryName(iso)}
                                    className={c(
                                        VT.toolButton.button,
                                        styles.flagButton,
                                        on ? styles.selected : "",
                                    )}
                                    onSelect={() =>
                                        onChange({ ...filters, countries: toggle(filters.countries, iso) })
                                    }
                                />
                            );
                        })}
                    </div>
                </div>
            )}
            {buttonRow(t("Theme", "Theme"), themes, filters.themes, "themes")}
            {buttonRow(t("Power", "Power"), energies, filters.energies, "energies")}

            {/* Authors as checkboxes, one per line inside a height-capped scroll box. The author
                count is unbounded -- it is however many creators the player subscribes to -- so
                the list scrolls rather than the panel growing: the panel keeps one size and the
                steppers below stay where the hand expects them. */}
            {authors.length > 1 && (
                <div className={styles.optionRow}>
                    <div className={styles.optionLabel}>{t("Author", "Author")}</div>
                    <VC.Scrollable vertical={true} className={styles.authorList}>
                        {authors.map((author) => {
                            const on = filters.authors.includes(author);
                            return (
                                <div key={author} className={styles.checkRow}>
                                    {/* The game's own checkbox -- the same widget the vehicle rows
                                        use -- rather than Find It's tool-button imitation of one.
                                        It brings its own border and tick, so it reads as a checkbox
                                        without any styling of ours. */}
                                    <VC.Checkbox
                                        checked={on}
                                        onChange={() =>
                                            onChange({
                                                ...filters,
                                                authors: toggle(filters.authors, author),
                                            })
                                        }
                                    />
                                    <div className={styles.checkLabel}>{author}</div>
                                </div>
                            );
                        })}
                    </VC.Scrollable>
                </div>
            )}

            {/* Rail lines only: hide stock longer than the shortest platform. A single toggle
                in the same button style, with the platform length on it so the cut-off is
                visible. */}
            {line.platformLength > 0 && (
                <div className={styles.optionRow}>
                    <div className={styles.optionLabel}>{t("Platform", "Platform")}</div>
                    <div className={styles.optionContent}>
                        <VC.ToolButton
                            src=""
                            selected={filters.fitsLine}
                            multiSelect={true}
                            className={c(
                                VT.toolButton.button,
                                styles.textButton,
                                filters.fitsLine ? styles.selected : "",
                            )}
                            onSelect={() => onChange({ ...filters, fitsLine: !filters.fitsLine })}
                        >
                            {t("FitsLine", "Fits ({LENGTH} m)", { LENGTH: line.platformLength })}
                        </VC.ToolButton>
                    </div>
                </div>
            )}

            {stepper(t("MinPassengers", "Min passengers"), filters.minPassengers, PASSENGER_STEP, (v) =>
                onChange({ ...filters, minPassengers: v }),
            )}
            {stepper(t("MinSpeed", "Min speed (km/h)"), filters.minSpeed, SPEED_STEP, (v) =>
                onChange({ ...filters, minSpeed: v }),
            )}

            {filtersActive(filters) && (
                <div className={styles.optionRow}>
                    <div className={styles.optionLabel} />
                    <div className={styles.optionContent}>
                        <VC.ToolButton
                            src=""
                            className={c(VT.toolButton.button, styles.textButton)}
                            onSelect={() => onChange(NO_FILTERS)}
                        >
                            Clear
                        </VC.ToolButton>
                    </div>
                </div>
            )}
        </div>
    );
};
