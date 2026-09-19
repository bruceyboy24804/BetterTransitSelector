import { useMemo } from "react";
import { FormattedParagraphs, MarkdownRenderer, Tooltip } from "cs2/ui";
import { VC, VT } from "vanilla/Components";
import { c } from "utils/classes";
import { openUrl } from "../bindings";
import { countriesOf, flagUrl, useCountryName, usePackInfo, useT, type StatsLookup, type VehiclePrefab } from "../vehicle-stats";
import { VehicleGroup } from "./vehicle-group";
import styles from "./pack-page.module.scss";

// The mod's own copies of PDX's three upload glyphs; the game's UI bundle ships none of them
// (the mod browser is the PDX SDK's own web view). Drawn tinted, like the info glyph.
const ICON_SUBSCRIBERS = "coui://bettertransitselector/icons/Subscribers.svg";
const ICON_LIKES = "coui://bettertransitselector/icons/Likes.svg";
const ICON_SIZE = "coui://bettertransitselector/icons/Size.svg";

/** 6512 -> "6.5k", the way PDX writes it. */
export const compact = (n: number) =>
    n >= 1_000_000 ? `${(n / 1_000_000).toFixed(1)}M` : n >= 1000 ? `${(n / 1000).toFixed(1)}k` : `${n}`;

/** Bytes -> "91.2 MB" / "640 KB". */
export const sizeOf = (bytes: number) =>
    bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;

export interface PackPageProps {
    /** The upload's group id, so the page knows whether the binding's data is for it yet. */
    group: string;
    title: string;
    members: VehiclePrefab[];
    stats: StatsLookup;
    nameOf: (vehicle: VehiclePrefab) => string;
    isSelected: (vehicle: VehiclePrefab) => boolean;
    isDisabled: (vehicle: VehiclePrefab) => boolean;
    showSecondary: boolean;
    onToggle: (vehicle: VehiclePrefab, selected: boolean) => void;
    /** Opens a vehicle's page from the pack's rows. */
    onRowInfo?: (vehicle: VehiclePrefab) => void;
    onClose: () => void;
}

/** "2026-07-08T22:04:41.000Z" -> "2026-07-08". No Intl in this engine; the ISO date is honest. */
const dateOf = (iso: string) => (iso ? iso.slice(0, 10) : "");

/**
 * An upload's page, beside the list: what the game's mod browser knows about it, plus the
 * pack's own vehicles as pickable rows and the union of their countries.
 *
 * Every field comes from the cache the game keeps for its mod browser, so there is no request
 * of ours. Images are the PDX CDN URLs that cache holds; the game's own browser renders them in
 * the main menu, and if the in-game view will not, they simply do not paint -- nothing else on
 * the page depends on them. Links open through C# in the system browser, as the game's do.
 */
export const PackPage = ({
    group,
    title,
    members,
    stats,
    nameOf,
    isSelected,
    isDisabled,
    showSecondary,
    onToggle,
    onRowInfo,
    onClose,
}: PackPageProps) => {
    const t = useT();
    const info = usePackInfo();
    const countryName = useCountryName();
    const renderer = useMemo(() => new MarkdownRenderer(), []);

    // The binding carries whichever upload was last requested; until it is this one, the page
    // is a skeleton of what the list already knows, not another upload's text.
    const ready = info.valid && info.group === group;

    const countries = useMemo(
        () => [...new Set(members.flatMap((v) => countriesOf(stats(v))))].sort(),
        [members, stats],
    );

    return (
        <div className={styles.page}>
            <div className={styles.header}>
                <div className={styles.title}>{ready ? info.title : title}</div>
                {/* Vanilla's own panel close, piece for piece: IconButton, tinted, the round
                    highlight theme, the panel theme's closeButton placement. */}
                <VC.IconButton
                    tinted={true}
                    src="Media/Glyphs/Close.svg"
                    theme={VT.roundHighlightButton}
                    className={c(VT.panel.closeButton, styles.close)}
                    onSelect={onClose}
                />
            </div>

            {ready && info.cover && (
                <div className={styles.cover} style={{ backgroundImage: `url(${info.cover})` }} />
            )}

            {ready && (
                <div className={styles.meta}>
                    <span>{info.author}</span>
                    {info.created && <span className={styles.dim}>{" · " + dateOf(info.created)}</span>}
                    {info.version && <span className={styles.dim}>{" · v" + info.version}</span>}
                </div>
            )}

            {/* The three figures PDX shows on an upload, with the same icons: subscribers,
                likes and size. "ratingsTotal" is the number of thumbs-up -- PDX has no star
                rating, and "rating" is 5 on every upload. */}
            {ready && (info.subscriptions > 0 || info.ratingsTotal > 0 || info.size > 0) && (
                <div className={styles.meta}>
                    {info.subscriptions > 0 && (
                        <Tooltip tooltip={t("Subscribers", "Subscribers")}>
                            <span className={styles.figure}>
                                <span className={styles.figureIcon} style={{ maskImage: `url(${ICON_SUBSCRIBERS})` }} />
                                {compact(info.subscriptions)}
                            </span>
                        </Tooltip>
                    )}
                    {info.ratingsTotal > 0 && (
                        <Tooltip tooltip={t("Likes", "Likes")}>
                            <span className={styles.figure}>
                                <span className={styles.figureIcon} style={{ maskImage: `url(${ICON_LIKES})` }} />
                                {compact(info.ratingsTotal)}
                            </span>
                        </Tooltip>
                    )}
                    {info.size > 0 && (
                        <Tooltip tooltip={t("Size", "Size")}>
                            <span className={styles.figure}>
                                <span className={styles.figureIcon} style={{ maskImage: `url(${ICON_SIZE})` }} />
                                {sizeOf(info.size)}
                            </span>
                        </Tooltip>
                    )}
                </div>
            )}

            {countries.length > 0 && (
                <div className={styles.flags}>
                    {countries.map((iso) => (
                        <Tooltip key={iso} tooltip={countryName(iso)}>
                            <div className={styles.flag} style={{ backgroundImage: `url(${flagUrl(iso)})` }} />
                        </Tooltip>
                    ))}
                </div>
            )}

            {ready && info.longDescription && (
                <div className={styles.description}>
                    <FormattedParagraphs
                        text={info.longDescription}
                        renderer={renderer}
                        onLinkSelect={openUrl}
                    />
                </div>
            )}

            {ready && info.screenshots.length > 0 && (
                <div className={styles.shots}>
                    {info.screenshots.map((url) => (
                        <div key={url} className={styles.shot} style={{ backgroundImage: `url(${url})` }} />
                    ))}
                </div>
            )}

            {ready && info.dependencies.length > 0 && (
                <div className={styles.section}>
                    <div className={styles.sectionHeading}>{t("Requires", "Requires")}</div>
                    {info.dependencies.map((d) => (
                        <div key={d.id} className={c(styles.dependency, d.installed ? "" : styles.missing)}>
                            {d.name}
                            {!d.installed && <span className={styles.dim}>{" · " + t("NotInstalled", "not installed")}</span>}
                        </div>
                    ))}
                </div>
            )}

            {ready && (info.links.length > 0 || info.forumLink) && (
                <div className={styles.links}>
                    {info.forumLink && (
                        <VC.ToolButton src="" className={c(VT.toolButton.button, styles.link)} onSelect={() => openUrl(info.forumLink)}>
                            {t("Forum", "Forum")}
                        </VC.ToolButton>
                    )}
                    {info.links.map((l) => (
                        <VC.ToolButton key={l.url} src="" className={c(VT.toolButton.button, styles.link)} onSelect={() => openUrl(l.url)}>
                            {l.type ? l.type.charAt(0).toUpperCase() + l.type.slice(1) : l.url}
                        </VC.ToolButton>
                    ))}
                </div>
            )}

            <div className={styles.section}>
                <div className={styles.sectionHeading}>{t("Vehicles", "Vehicles")}</div>
                <VehicleGroup
                    title={ready ? info.title : title}
                    members={members}
                    forceOpen={true}
                    isSelected={isSelected}
                    isDisabled={isDisabled}
                    showSecondary={showSecondary}
                    stats={stats}
                    nameOf={nameOf}
                    onToggle={onToggle}
                    starKey={group}
                    isPack={true}
                    onRowInfo={onRowInfo}
                />
            </div>
        </div>
    );
};
