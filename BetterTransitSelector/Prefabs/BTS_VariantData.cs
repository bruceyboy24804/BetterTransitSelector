namespace BetterTransitSelector.Prefabs {
    #region Using Statements

    using Unity.Entities;

    #endregion

    /// <summary>
    /// Marks a vehicle as one variant of a family, and names the placeholder that identifies it.
    /// </summary>
    /// <remarks>
    /// The asset creators' design, and deliberately the inverse of any list: nothing declares what a
    /// family contains, because an author cannot know today which liveries will exist in a year.
    /// Each vehicle points at the family's placeholder, and the family is whatever points at it.
    ///
    /// The placeholder is a pack (<see cref="BTS_Pack"/>) shipped in the upload. It has no
    /// geometry or behaviour, so it is inert for players without this mod, and its identity — not a
    /// name, not a typed id — is what links uploads: the same asset included as-is in a second
    /// upload is the same family.
    /// </remarks>
    public struct BTS_VariantData : IComponentData, IQueryTypeParameter {
        /// <summary>The placeholder asset identifying the family.</summary>
        public Entity m_Placeholder;

        /// <summary>
        /// Sort order within the family, low first.
        /// </summary>
        /// <remarks>
        /// An index, not a position in a list: leave gaps and a livery uploaded years later slots
        /// in where it belongs without anything existing being republished.
        /// </remarks>
        public int m_Index;

        /// <summary><see cref="Country"/> flags as a bitmask; 0 when the creator declared none.</summary>
        public ulong m_Countries;
    }
}
