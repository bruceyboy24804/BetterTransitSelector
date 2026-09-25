import { useMemo } from "react";
import { FormattedParagraphs, MarkdownRenderer, Tooltip } from "cs2/ui";
import { VC, VT } from "vanilla/Components";
import { c } from "utils/classes";
import { openUrl } from "../bindings";
import { countriesOf, flagUrl, useCountryName, usePackInfo, useT, type StatsLookup, type VehiclePrefab } from "../vehicle-stats";
import { VehicleGroup } from "./vehicle-group";
import styles from "./pack-page.module.scss";

const ICON_SUBSCRIBERS = "coui://bettertransitselector/icons/Subscribers.svg";
const ICON_LIKES = "coui://bettertransitselector/icons/Likes.svg";
const ICON_SIZE = "coui://bettertransitselector/icons/Size.svg";

export const compact = (n: number) =>
    n >= 1_000_000 ? `${(n / 1_000_000).toFixed(1)}M` : n >= 1000 ? `${(n / 1000).toFixed(1)}k` : `${n}`;

export const sizeOf = (bytes: number) =>
    bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;

export interface PackPageProps {
    group: string;
    title: string;
    members: VehiclePrefab[];
    stats: StatsLookup;
    nameOf: (vehicle: VehiclePrefab) => string;
    isSelected: (vehicle: VehiclePrefab) => boolean;
    isDisabled: (vehicle: VehiclePrefab) => boolean;
    showSecondary: boolean;
    onToggle: (vehicle: VehiclePrefab, selected: boolean) => void;

    onRowInfo?: (vehicle: VehiclePrefab) => void;
    onClose: () => void;
}

const dateOf = (iso: string) => (iso ? iso.slice(0, 10) : "");

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

    const ready = info.valid && info.group === group;

    const countries = useMemo(
        () => [...new Set(members.flatMap((v) => countriesOf(stats(v))))].sort(),
        [members, stats],
    );

    return (
        <div className={styles.page}>
            <div className={styles.header}>
                <div className={styles.title}>{ready ? info.title : title}</div>

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
