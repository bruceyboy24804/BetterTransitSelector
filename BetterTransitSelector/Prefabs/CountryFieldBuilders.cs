namespace BetterTransitSelector.Prefabs {
    #region Using Statements

    using System;

    using Game.Reflection;
    using Game.UI.Widgets;

    #endregion

    /// <summary>
    /// The editor field for <see cref="Country"/>: the game's own flags dropdown, capped at five.
    /// </summary>
    /// <remarks>
    /// The editor builds a field for every prefab member by asking the factories in
    /// <c>EditorGenerator.kFactories</c> in order; this one is inserted at the front at load
    /// (<see cref="Register"/>) and answers only for <see cref="Country"/>, so every other type
    /// still goes to the game's factories.
    ///
    /// It returns the same <c>FlagsField</c> the game would, with the same members, but the
    /// accessor between the widget and the prefab field is <see cref="CappedAccessor"/>: a write
    /// that would set more than <see cref="CountryCodes.kMaxPerVehicle"/> bits is dropped and the
    /// old value stays. That is the whole enforcement -- the sixth tick does nothing, and the
    /// widget's select-all button (which the frontend draws unconditionally and a mod cannot
    /// remove) lands on nothing too, which is what the creators asked for.
    /// </remarks>
    public sealed class CountryFieldBuilders : IFieldBuilderFactory {
        private static bool s_Registered;

        /// <summary>Puts this factory ahead of the game's, once.</summary>
        public static void Register() {
            if (s_Registered) {
                return;
            }

            EditorGenerator.kFactories.Insert(0, new CountryFieldBuilders());
            s_Registered = true;
        }

        /// <inheritdoc/>
        public FieldBuilder TryCreate(Type memberType, object[] attributes) {
            if (memberType != typeof(Country)) {
                return null;
            }

            // The game's builder gives the FlagsField with the members already resolved; only the
            // accessor is swapped for the capped one.
            var inner = EnumFieldBuilders.Create(memberType, attributes);
            if (inner == null) {
                return null;
            }

            return accessor => {
                var widget = inner(accessor);
                if (widget is FlagsField flags) {
                    flags.accessor = new CappedAccessor(flags.accessor, CountryCodes.kMaxPerVehicle);
                }
                return widget;
            };
        }

        /// <summary>A ulong accessor that refuses writes with more than a set number of bits.</summary>
        private sealed class CappedAccessor : ITypedValueAccessor<ulong>, IValueAccessor {
            private readonly ITypedValueAccessor<ulong> m_Inner;
            private readonly int m_Max;

            public CappedAccessor(ITypedValueAccessor<ulong> inner, int max) {
                m_Inner = inner;
                m_Max   = max;
            }

            public Type valueType => typeof(ulong);

            public IValueAccessor parent => m_Inner.parent;

            public object GetValue() => m_Inner.GetTypedValue();

            public void SetValue(object value) => SetTypedValue((ulong)value);

            public ulong GetTypedValue() => m_Inner.GetTypedValue();

            public void SetTypedValue(ulong value) {
                if (CountryCodes.Count(value) <= m_Max) {
                    m_Inner.SetTypedValue(value);
                }
            }
        }
    }
}
