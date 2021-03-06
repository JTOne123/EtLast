﻿namespace FizzCode.EtLast.Specialized
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    public class HungarianTaxNumberConverter : StringConverter
    {
        /// <summary>
        /// Default true.
        /// </summary>
        public bool AutomaticallyAddHyphens { get; set; } = true;

        public static Dictionary<int, string> RegionNames { get; } = CreateRegionNamesDictionary();
        private static readonly int[] _checkSumNumbers = new[] { 9, 7, 3, 1, 9, 7, 3 };

        public HungarianTaxNumberConverter(string formatHint = null, IFormatProvider formatProviderHint = null)
            : base(formatHint, formatProviderHint)
        {
            RemoveSpaces = true;
            RemoveLineBreaks = true;
            TrimStartEnd = true;
        }

        public override object Convert(object source)
        {
            var taxNr = base.Convert(source) as string;

            return Convert(taxNr, AutomaticallyAddHyphens);
        }

        public static string Convert(string taxNr, bool automaticallyAddHyphens)
        {
            if (string.IsNullOrEmpty(taxNr))
                return null;

            if (!Validate(taxNr))
                return null;

            if (automaticallyAddHyphens && taxNr.Length == 11 && !taxNr.Contains("-", StringComparison.InvariantCultureIgnoreCase))
            {
                taxNr = taxNr.Substring(0, 8) + "-" + taxNr.Substring(8, 1) + "-" + taxNr.Substring(9, 2);
            }

            return taxNr;
        }

        public static bool Validate(string taxNr)
        {
            string[] parts;

            if (!taxNr.Contains("-", StringComparison.InvariantCultureIgnoreCase))
            {
                if (taxNr.Length != 11)
                    return false;

                parts = new[] { taxNr.Substring(0, 8), taxNr.Substring(8, 1), taxNr.Substring(9, 2) };
            }
            else
            {
                parts = taxNr.Split('-');
                if (parts.Length != 3 || parts[0].Length != 8 || parts[1].Length != 1 || parts[2].Length != 2)
                    return false;
            }

            if (!int.TryParse(parts[1], out var vatType))
                return false;

            if (vatType < 1 || vatType > 5)
                return false;

            if (!int.TryParse(parts[2], out var region))
                return false;

            if (!RegionNames.ContainsKey(region))
                return false;

            var digitSum = 0;
            for (var i = 0; i < 7; i++)
            {
                if (!int.TryParse(parts[0][i].ToString(CultureInfo.InvariantCulture), out var digit))
                    return false;

                digitSum += digit * _checkSumNumbers[i];
            }

            var checkSum = digitSum % 10;
            if (checkSum != 0)
                checkSum = 10 - checkSum;

            if (!int.TryParse(parts[0][7].ToString(CultureInfo.InvariantCulture), out var lastDigit))
                return false;

            if (lastDigit != checkSum)
                return false;

            return true;
        }

        private static Dictionary<int, string> CreateRegionNamesDictionary()
        {
            return new Dictionary<int, string>
            {
                [2] = "Baranya",
                [22] = "Baranya",
                [3] = "Bács-Kiskun",
                [23] = "Bács-Kiskun",
                [4] = "Békés",
                [24] = "Békés",
                [5] = "Borsod-Abaúj-Zemplén",
                [25] = "Borsod-Abaúj-Zemplén",
                [6] = "Csongrád",
                [26] = "Csongrád",
                [7] = "Fejér",
                [27] = "Fejér",
                [8] = "Győr-Moson-Sopron",
                [28] = "Győr-Moson-Sopron",
                [9] = "Hajdú-Bihar",
                [29] = "Hajdú-Bihar",
                [10] = "Heves",
                [30] = "Heves",
                [11] = "Komárom-Esztergom",
                [31] = "Komárom-Esztergom",
                [12] = "Nógrád",
                [32] = "Nógrád",
                [13] = "Pest",
                [33] = "Pest",
                [14] = "Somogy",
                [34] = "Somogy",
                [15] = "Szabolcs-Szatmár-Bereg",
                [35] = "Szabolcs-Szatmár-Bereg",
                [16] = "Jász-Nagykun-Szolnok",
                [36] = "Jász-Nagykun-Szolnok",
                [17] = "Tolna",
                [37] = "Tolna",
                [18] = "Vas",
                [38] = "Vas",
                [19] = "Veszprém",
                [39] = "Veszprém",
                [20] = "Zala",
                [40] = "Zala",
                [41] = "Észak-Budapest",
                [42] = "Kelet-Budapest",
                [43] = "Dél-Budapest",
                [44] = "Kiemelt Adózók Adóigazgatósága",
                [51] = "Kiemelt Ügyek Adóigazgatósága",
            };
        }
    }
}