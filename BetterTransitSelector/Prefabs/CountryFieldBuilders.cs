namespace BetterTransitSelector.Prefabs {
    #region Using Statements

    using System;

    using Game.Reflection;
    using Game.UI.Widgets;

    #endregion

    public sealed class CountryFieldBuilders : IFieldBuilderFactory {
        private static bool s_Registered;

        public static void Register() {
            if (s_Registered) {
                return;
            }

            EditorGenerator.kFactories.Insert(0, new CountryFieldBuilders());
            s_Registered = true;
        }

        public FieldBuilder TryCreate(Type memberType, object[] attributes) {
            if (memberType != typeof(Country)) {
                return null;
            }

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
