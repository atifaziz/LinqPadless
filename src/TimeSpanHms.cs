#region Copyright (c) 2019 Atif Aziz. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

namespace LinqPadless
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;

    static class TimeSpanHms
    {
        public static TimeSpan Parse(string input) =>
            TryParse(input, out var value) ? value : throw new FormatException();

        public static bool TryParse(string input, out TimeSpan value)
        {
            var match =
                Regex.Match(input,
                    @"^(   (?<h>[0-9]+) : (?<m>[0-9]+) : (?<s>[0-9]+)
                         | (?<h>[0-9]+)[hH] ((?<m>[0-9]+)[mM])? ((?<s>[0-9]+)[sS])?
                         | (?<m>[0-9]+)[mM] ((?<s>[0-9]+)[sS])?
                         | (?<s>[0-9]+)[sS]
                         )$",
                    RegexOptions.IgnorePatternWhitespace |
                    RegexOptions.ExplicitCapture);

            switch (match.Success, match.Groups)
            {
                case (true, var groups):
                    var h = ParseToken(groups["h"].Value);
                    var m = ParseToken(groups["m"].Value);
                    var s = ParseToken(groups["s"].Value);
                    value = new TimeSpan(h, m, s);
                    return true;

                default:
                    value = default;
                    return false;
            }

            static int ParseToken(string s)
                => s.Length > 0
                 ? int.Parse(s, NumberStyles.None, CultureInfo.InvariantCulture)
                 : 0;
        }

        public static string FormatHms(this TimeSpan duration)
        {
            return string.Concat(Format(duration.Days        , "d" ),
                                 Format(duration.Hours       , "h" ),
                                 Format(duration.Minutes     , "m" ),
                                 Format(duration.Seconds     , "s" ),
                                 Format(duration.Milliseconds, "ms"));

            static string Format(int n, string unit) =>
                n > 0 ? n.ToString(CultureInfo.InvariantCulture) + unit : string.Empty;
        }
    }
}
