namespace BetterTransitSelector.Prefabs {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    #endregion

    /// <summary>
    /// Countries a vehicle can plausibly operate in, as a creator declares them.
    /// </summary>
    /// <remarks>
    /// The creators' replacement for the game's theme: "which trains thematically fit my build",
    /// shown as flags.
    ///
    /// A flags enum so the editor offers one multi-select dropdown, and so a bitmask travels through
    /// ECS as one <c>ulong</c>. Members are full names because the editor shows the member name
    /// verbatim (<c>EnumFieldBuilders.BuildMembers</c>); the ISO code each maps to is in
    /// <see cref="CountryCodes"/> and is what the UI receives and the flag file is named after.
    ///
    /// The editor's flags widget carries a select-all button the creators did not want anyone near,
    /// and a cap of five was asked for. Neither is possible from the enum alone; both come from
    /// <see cref="CountryFieldBuilders"/>, which supplies the widget with an accessor that refuses
    /// any value with more than five bits, so select-all lands on nothing.
    ///
    /// Bits are never reassigned: they are what a saved asset carries.
    /// </remarks>
    [Flags]
    public enum Country : ulong {
        None = 0,

        // Europe
        Germany = 1UL << 0,
        France = 1UL << 1,
        Italy = 1UL << 2,
        Spain = 1UL << 3,
        UnitedKingdom = 1UL << 4,
        Netherlands = 1UL << 5,
        Belgium = 1UL << 6,
        Switzerland = 1UL << 7,
        Austria = 1UL << 8,
        Poland = 1UL << 9,
        Czechia = 1UL << 10,
        Sweden = 1UL << 11,
        Norway = 1UL << 12,
        Denmark = 1UL << 13,
        Finland = 1UL << 14,
        Portugal = 1UL << 15,
        Hungary = 1UL << 16,
        Slovakia = 1UL << 17,
        Romania = 1UL << 18,
        Ireland = 1UL << 19,
        Luxembourg = 1UL << 20,
        Croatia = 1UL << 21,
        Slovenia = 1UL << 22,
        Greece = 1UL << 23,

        // Rest of world
        UnitedStates = 1UL << 32,
        Canada = 1UL << 33,
        Japan = 1UL << 34,
        China = 1UL << 35,
        SouthKorea = 1UL << 36,
        India = 1UL << 37,
        Russia = 1UL << 38,
        Australia = 1UL << 39,
    }

    /// <summary>ISO 3166-1 alpha-2 code per <see cref="Country"/>; also the flag file name.</summary>
    public static class CountryCodes {
        /// <summary>How many countries one vehicle may declare.</summary>
        public const int kMaxPerVehicle = 5;

        private static readonly Dictionary<Country, string> kCodes = new Dictionary<Country, string> {
            { Country.Germany, "DE" }, { Country.France, "FR" }, { Country.Italy, "IT" },
            { Country.Spain, "ES" }, { Country.UnitedKingdom, "GB" }, { Country.Netherlands, "NL" },
            { Country.Belgium, "BE" }, { Country.Switzerland, "CH" }, { Country.Austria, "AT" },
            { Country.Poland, "PL" }, { Country.Czechia, "CZ" }, { Country.Sweden, "SE" },
            { Country.Norway, "NO" }, { Country.Denmark, "DK" }, { Country.Finland, "FI" },
            { Country.Portugal, "PT" }, { Country.Hungary, "HU" }, { Country.Slovakia, "SK" },
            { Country.Romania, "RO" }, { Country.Ireland, "IE" }, { Country.Luxembourg, "LU" },
            { Country.Croatia, "HR" }, { Country.Slovenia, "SI" }, { Country.Greece, "GR" },
            { Country.UnitedStates, "US" }, { Country.Canada, "CA" }, { Country.Japan, "JP" },
            { Country.China, "CN" }, { Country.SouthKorea, "KR" }, { Country.India, "IN" },
            { Country.Russia, "RU" }, { Country.Australia, "AU" },
        };

        /// <summary>The code, or null for <see cref="Country.None"/> or an unknown value.</summary>
        public static string Of(Country country) =>
            kCodes.TryGetValue(country, out var code) ? code : null;

        /// <summary>Set bits in a mask; a mask with more than <see cref="kMaxPerVehicle"/> is over the cap.</summary>
        public static int Count(ulong mask) {
            var n = 0;
            while (mask != 0) {
                mask &= mask - 1;
                n++;
            }
            return n;
        }
    }
}
