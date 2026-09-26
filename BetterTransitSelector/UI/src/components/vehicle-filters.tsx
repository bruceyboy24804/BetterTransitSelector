import { useState } from "react";
import { VC, VT } from "vanilla/Components";
import { c } from "utils/classes";
import {
    NO_FILTERS,
    filtersActive,
    flagUrl,
    useCountryName,
    useStatOptions,
    useT,
    type VehicleFilters,
} from "../vehicle-stats";
import { setSorting } from "../bindings";
import styles from "./vehicle-filters.module.scss";

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

const PASSENGER_STEP = 50;
const SPEED_STEP = 20;

export interface VehicleFiltersBarProps {
    filters: VehicleFilters;
    onChange: (filters: VehicleFilters) => void;

    themes: string[];
    energies: string[];
    authors: string[];
    sources: string[];
    countries: string[];
}

const toggle = (values: string[], value: string): string[] =>
    values.includes(value) ? values.filter((v) => v !== value) : [...values, value];

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
    const sourceLabel = (v: string) => t(`Source.${v}`, v);

    const buttonRow = (
        label: string,
        values: string[],
        selected: string[],
        key: "themes" | "energies" | "sources",

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

            {buttonRow(t("Source", "Source"), sources, filters.sources, "sources", sourceLabel)}

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

            {authors.length > 1 && (
                <div className={styles.optionRow}>
                    <div className={styles.optionLabel}>{t("Author", "Author")}</div>
                    <VC.Scrollable vertical={true} className={styles.authorList}>
                        {authors.map((author) => {
                            const on = filters.authors.includes(author);
                            return (
                                <div key={author} className={styles.checkRow}>

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
