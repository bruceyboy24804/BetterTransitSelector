namespace BetterTransitSelector.Prefabs {
    #region Using Statements

    using Unity.Entities;

    #endregion

    public struct BTS_PackData : IComponentData, IQueryTypeParameter { }

    [InternalBufferCapacity(0)]
    public struct BTS_PackElement : IBufferElementData {
        public Entity m_Vehicle;

        public BTS_PackElement(Entity vehicle) {
            m_Vehicle = vehicle;
        }
    }
}
