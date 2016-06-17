#region Copyright (c) 2014 Atif Aziz. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
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
    using System.Diagnostics;
    using System.Linq;

    // ReSharper disable once PartialTypeWithSinglePart

    static partial class Seq
    {
        /// <summary>
        /// Splits the sequence into two sequences, containing the elements
        /// for which the given predicate returns <c>true</c> and <c>false</c>
        /// respectively.
        /// </summary>

        public static TResult Partition<T, TResult>(this IEnumerable<T> source,
            Func<T, bool> predicate,
            Func<IEnumerable<T>, IEnumerable<T>, TResult> selector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            IEnumerable<T> fs = null;
            IEnumerable<T> ts = null;

            foreach (var g in source.GroupBy(predicate))
            {
                if (g.Key)
                {
                    Debug.Assert(ts == null);
                    ts = g;
                }
                else
                {
                    Debug.Assert(fs == null);
                    fs = g;
                }
            }

            return selector(fs ?? Enumerable.Empty<T>(),
                            ts ?? Enumerable.Empty<T>());
        }
    }
}
