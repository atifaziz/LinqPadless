#region Copyright (c) 2016 Atif Aziz. All rights reserved.
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
    using System.Collections.Generic;
    using System.IO;
    using Mono.Options;

    static class StringExtensions
    {
        public static IEnumerable<string> Lines(this string input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return _(); IEnumerable<string> _()
            {
                using (var reader = new StringReader(input))
                {
                    while (reader.ReadLine() is string line)
                        yield return line;
                }
            }
        }
    }

    static class OptionSetExtensions
    {
        public static T WriteOptionDescriptionsReturningWriter<T>(this OptionSet options, T writer)
            where T : TextWriter
        {
            options.WriteOptionDescriptions(writer);
            return writer;
        }
    }
}
