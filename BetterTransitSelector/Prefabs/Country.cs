namespace BetterTransitSelector.Prefabs {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    #endregion

    [Flags]
    public enum Country : ulong {
        None = 0,

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

        UnitedStates = 1UL << 32,
        Canada = 1UL << 33,
        Japan = 1UL << 34,
        China = 1UL << 35,
        SouthKorea = 1UL << 36,
        India = 1UL << 37,
        Russia = 1UL << 38,
        Australia = 1UL << 39,
    }

    public static class CountryCodes {
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

        public static string Of(Country country) =>
            kCodes.TryGetValue(country, out var code) ? code : null;

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
