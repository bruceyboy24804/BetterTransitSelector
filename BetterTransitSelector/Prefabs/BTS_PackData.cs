namespace BetterTransitSelector.Prefabs {
    #region Using Statements

    using Unity.Entities;

    #endregion

    /// <summary>
    /// Marks a prefab entity as a train pack: a placeholder that heads a family and is not a vehicle.
    /// </summary>
    /// <remarks>
    /// The counterpart of vanilla's <c>PlaceholderObjectData</c>. A tag: the pack carries no
    /// settings, because a family's identity is the pack asset itself and its heading is the pack's
    /// name. Not serialized — it lives on a prefab entity the game rebuilds from the asset on load.
    /// </remarks>
    public struct BTS_PackData : IComponentData, IQueryTypeParameter { }

    /// <summary>
    /// One variant registered under a pack.
    /// </summary>
    /// <remarks>
    /// The counterpart of vanilla's <c>PlaceholderObjectElement</c>, and built the same way: the pack
    /// starts with an empty list, and at load every vehicle whose <see cref="BTS_VariantData"/>
    /// names this pack is appended to it. The pack never declares its members — which is the
    /// creators' requirement that there be no list, since a list would fix the variants at the
    /// moment the first one is published.
    /// </remarks>
    [InternalBufferCapacity(0)]
    public struct BTS_PackElement : IBufferElementData {
        /// <summary>The vehicle prefab entity registered as a variant of this pack.</summary>
        public Entity m_Vehicle;

        public BTS_PackElement(Entity vehicle) {
            m_Vehicle = vehicle;
        }
    }
}
