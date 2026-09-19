namespace BetterTransitSelector.Prefabs {
    #region Using Statements

    using System.Collections.Generic;

    using Game.Prefabs;
    using Game.UI.Editor;
    using Game.UI.Widgets;

    using Unity.Entities;

    #endregion

    /// <summary>
    /// Marks a <see cref="GroupPrefab"/> as a train pack: the placeholder that heads a family of
    /// variants and is not a train.
    /// </summary>
    /// <remarks>
    /// The asset creators' long-term goal — "a headline or parent asset that is not a functional
    /// train, might be useful to bundle freight cars or engines. Essentially a placeholder."
    ///
    /// Why a component on a vanilla prefab type rather than a prefab type of our own: an asset's
    /// root type must exist for the asset to load at all. <c>PrefabAsset.Load</c> deserialises
    /// resiliently and then casts the result to <c>ScriptableObject</c> — with the mod disabled a
    /// root of a mod type comes back as nothing castable and the game shows
    /// <c>InvalidCastException: Specified cast is not valid</c> for every pack in every
    /// subscribed upload (Maestro hit exactly this). A <em>component</em> of a missing type, by
    /// contrast, is dropped: <c>PrefabBase.OnDeserialized</c> runs
    /// <c>components.RemoveAll(x =&gt; x == null)</c>. So the root is the game's
    /// <see cref="GroupPrefab"/> — an empty, inert type the game itself uses only as an identity
    /// token (prop randomisation groups) — and everything of ours is this component, which
    /// vanishes cleanly when the mod is off, leaving an inert asset in the package.
    ///
    /// A pack's identity is its asset GUID, and the trains point at it through
    /// <see cref="BTS_Variant.m_Placeholder"/>. It carries no vehicle components, so the
    /// selector can never list it as pickable. Its name is the family's heading; the pack's own
    /// icon lives on the game's UI Object component beside this one.
    ///
    /// The mod registers a template at load so a creator can duplicate it in the editor.
    /// </remarks>
    [ComponentMenu("Better Transit Selector/", new[] { typeof(GroupPrefab) })]
    public class BTS_Pack : ComponentBase {
        /// <summary>
        /// An icon for the whole upload's folder, separate from this pack's own icon (which is on
        /// its UI Object component and heads the family). Set it on one pack in the upload.
        /// </summary>
        /// <remarks>
        /// About presentation only. An upload has no asset of its own to carry an icon -- the
        /// "Mod:&lt;id&gt;" prefab is the game's -- so a pack inside it is where a creator can put
        /// one. Its own field rather than "reuse my family icon", so the folder can look different
        /// from every family in it. Same picker as UI Object's icon.
        ///
        /// Fallbacks when empty: the upload's single iconed pack, then a vehicle thumbnail.
        /// </remarks>
        [CustomField(typeof(UIIconField))]
        public string m_UploadIcon;

        /// <summary>
        /// A pack this pack sits inside, or empty. The top-most pack in the chain becomes the
        /// picker's folder, in place of the upload.
        /// </summary>
        /// <remarks>
        /// Maestro's case: one upload per model (Mireo alone, BR612 alone), all wanted in one
        /// folder in game. The upload cannot be that folder -- it is one upload. So a pack can
        /// name a parent pack, and a creator ships the same parent asset in every upload, exactly
        /// as the same placeholder shipped in two uploads already joins their variants. Identity
        /// is the asset again: no id to type, and nobody can file under a collection they were
        /// not handed. The parent's name heads the folder, its UI Object icon is the folder's
        /// icon, and <see cref="m_UploadIcon"/> on it is honoured the same way.
        ///
        /// Optional; a pack without one is filed under its upload as before.
        /// </remarks>
        public GroupPrefab m_Parent;

        /// <summary>
        /// A heading to file this pack under, by name -- e.g. "DB Regio BaWü Trains". Every pack
        /// whose display name matches (case and spacing ignored) lists under one folder, whoever made it.
        /// Empty for none.
        /// </summary>
        /// <remarks>
        /// The convenient path REV0 asked for: two creators type the same text and their models
        /// share a heading, with no file to pass around. A shared <see cref="m_Parent"/> asset is
        /// the locked alternative. When both are set, the parent's display name or prefab name is what counts,
        /// and this field is ignored on the child.
        ///
        /// On a parent pack (one others name as Parent) this is the folder's display name when the
        /// prefab name is not fit to show -- "BTSPackParent_DBRegioBwegt" versus "DB Regio BaWü
        /// Trains".
        ///
        /// Heading and icon come from the oldest published member, so they do not change as later
        /// uploads join.
        /// </remarks>
        public string m_DisplayName;

        /// <summary>
        /// Keep this pack's folder to itself: never merge it with other packs that carry the same
        /// display name. Only meaningful on a parent pack (one others name as Parent).
        /// </summary>
        /// <remarks>
        /// REV0's condition for name-based headings: "if someone parks a brand and makes sketchfab
        /// dumps on it, I'd like to stay away from being bundled together". By default a Parent
        /// folder merges with anything of the same name -- that is what heals two creators who
        /// independently made the same heading. Exclusive keeps the folder keyed on this asset's
        /// GUID, so only packs that name this very asset as Parent are in it. Someone else typing
        /// the same display name gets a second folder of that name, next to it, not inside it.
        /// </remarks>
        public bool m_Exclusive;

        /// <inheritdoc/>
        /// <remarks>The parent must be loaded and have an entity before the chain is walked.</remarks>
        public override void GetDependencies(List<PrefabBase> prefabs) {
            base.GetDependencies(prefabs);

            if (m_Parent != null) {
                prefabs.Add(m_Parent);
            }
        }

        /// <inheritdoc/>
        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            components.Add(ComponentType.ReadWrite<BTS_PackData>());
            // The derived member list, filled at load from the variants that name this pack.
            components.Add(ComponentType.ReadWrite<BTS_PackElement>());
        }

        /// <inheritdoc/>
        /// <remarks>Nothing is ever spawned from a pack.</remarks>
        public override void GetArchetypeComponents(HashSet<ComponentType> components) { }
    }
}
