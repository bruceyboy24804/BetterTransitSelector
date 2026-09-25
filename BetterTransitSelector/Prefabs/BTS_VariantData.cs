namespace BetterTransitSelector.Prefabs {
    #region Using Statements

    using Unity.Entities;

    #endregion

    public struct BTS_VariantData : IComponentData, IQueryTypeParameter {
        public Entity m_Placeholder;

        public int m_Index;

        public ulong m_Countries;
    }
}
