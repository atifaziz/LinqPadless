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
    #region Imports

    using System;
    using System.Collections.Generic;
    using System.Linq;

    #endregion

    static partial class Seq
    {
        public static IEnumerable<T> Return<T>(params T[] items) => items;

        public static IEnumerable<T> Filter<T>(this IEnumerable<T> source) =>
            from item in source where item != null select item;

        static void Read<T>(ref IEnumerator<T> e, out T item)
        {
            if (e != null && e.MoveNext())
            {
                item = e.Current;
            }
            else
            {
                if (e != null)
                {
                    e.Dispose();
                    e = null;
                }

                item = default;
            }
        }

        public static void Deconstruct<T>(this IEnumerable<T> source, out T item1, out T item2)
        {
            using (var e = source.GetEnumerator())
            {
                var ee = e;
                Read(ref ee, out item1);
                Read(ref ee, out item2);
            }
        }
    }
}
