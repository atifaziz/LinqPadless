#region The MIT License (MIT)
//
// Copyright (c) 2013 Atif Aziz. All rights reserved.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
#endregion

namespace Gini
{
    #region Imports

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;

    #endregion

    #if GINI_PUBLIC
    public partial class Ini { }
    #endif
    
    static partial class Ini
    {
        static class Parser
        {
            static readonly Regex Regex;
            static readonly int SectionNumber;
            static readonly int KeyNumber;
            static readonly int ValueNumber;

            static Parser()
            {
                var re = Regex =
                    new Regex(@"^ *(\[(?<s>[a-z0-9-._][a-z0-9-._ ]*)\]|(?<k>[a-z0-9-._][a-z0-9-._ ]*)= *(?<v>[^\r\n]*))\s*$",
                        RegexOptions.Multiline
                        | RegexOptions.IgnoreCase
                        | RegexOptions.CultureInvariant);
                SectionNumber = re.GroupNumberFromName("s");
                KeyNumber = re.GroupNumberFromName("k");
                ValueNumber = re.GroupNumberFromName("v");
            }

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public static IEnumerable<T> Parse<T>(string ini, Func<string, string, string, T> selector)
            {
                return from Match m in Regex.Matches(ini ?? string.Empty)
                       select m.Groups into g
                       select selector(g[SectionNumber].Value.TrimEnd(),
                                       g[KeyNumber].Value.TrimEnd(),
                                       g[ValueNumber].Value.TrimEnd());
            }
        }

        public static IEnumerable<IGrouping<string, KeyValuePair<string, string>>> Parse(string ini)
        {
            return Parse(ini, KeyValuePair.Create);
        }

        public static IEnumerable<IGrouping<string, T>> Parse<T>(string ini, Func<string, string, T> settingSelector)
        {
            return Parse(ini, (_, k, v) => settingSelector(k, v));
        }

        public static IEnumerable<IGrouping<string, T>> Parse<T>(string ini, Func<string, string, string, T> settingSelector)
        {
            if (settingSelector == null) throw new ArgumentNullException("settingSelector");

            ini = ini.Trim();
            if (string.IsNullOrEmpty(ini))
                return Enumerable.Empty<IGrouping<string, T>>();

            var entries =
                from ms in new[]
                {
                    Parser.Parse(ini, (s, k, v) => new
                    {
                        Section = s,
                        Setting = KeyValuePair.Create(k, v)
                    })
                }
                from p in Enumerable.Repeat(new { Section = (string) null, 
                                                  Setting = KeyValuePair.Create(string.Empty, string.Empty) }, 1)
                                    .Concat(ms)
                                    .GroupAdjacent(s => s.Section == null || s.Section.Length > 0)
                                    .Pairwise((prev, curr) => new { Prev = prev, Curr = curr })
                where p.Prev.Key
                select KeyValuePair.Create(p.Prev.Last().Section, p.Curr) into e
                from s in e.Value
                select KeyValuePair.Create(e.Key, settingSelector(e.Key, s.Setting.Key, s.Setting.Value));

            return entries.GroupAdjacent(e => e.Key, e => e.Value);
        }

        public static IDictionary<string, IDictionary<string, string>> ParseHash(string ini)
        {
            return ParseHash(ini, null);
        }

        public static IDictionary<string, IDictionary<string, string>> ParseHash(string ini, IEqualityComparer<string> comparer)
        {
            comparer = comparer ?? StringComparer.OrdinalIgnoreCase;
            var sections = Parse(ini);
            return sections.GroupBy(g => g.Key ?? string.Empty, comparer)
                           .ToDictionary(g => g.Key,
                                         g => (IDictionary<string, string>) g.SelectMany(e => e)
                                                                             .GroupBy(e => e.Key, comparer)
                                                                             .ToDictionary(e => e.Key, e => e.Last().Value, comparer),
                                         comparer);
        }

        public static IDictionary<string, string> ParseFlatHash(string ini, Func<string, string, string> keyMerger)
        {
            return ParseFlatHash(ini, keyMerger, null);
        }

        public static IDictionary<string, string> ParseFlatHash(string ini, Func<string, string, string> keyMerger, IEqualityComparer<string> comparer)
        {
            if (keyMerger == null) throw new ArgumentNullException("keyMerger");

            var settings = new Dictionary<string, string>(comparer ?? StringComparer.OrdinalIgnoreCase);
            foreach (var setting in from section in Parse(ini)
                                    from setting in section
                                    select KeyValuePair.Create(keyMerger(section.Key, setting.Key), setting.Value))
            {
                settings[setting.Key] = setting.Value;
            }
            return settings;
        }

        static class KeyValuePair
        {
            public static KeyValuePair<TKey, TValue> Create<TKey, TValue>(TKey key, TValue value)
            {
                return new KeyValuePair<TKey, TValue>(key, value);
            }
        }

        #region MoreLINQ

        // MoreLINQ - Extensions to LINQ to Objects
        // Copyright (c) 2008 Jonathan Skeet. All rights reserved.
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

        static IEnumerable<TResult> Pairwise<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TSource, TResult> resultSelector)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (resultSelector == null) throw new ArgumentNullException("resultSelector");
            return PairwiseImpl(source, resultSelector);
        }

        static IEnumerable<TResult> PairwiseImpl<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TSource, TResult> resultSelector)
        {
            Debug.Assert(source != null);
            Debug.Assert(resultSelector != null);

            using (var e = source.GetEnumerator())
            {
                if (!e.MoveNext())
                    yield break;

                var previous = e.Current;
                while (e.MoveNext())
                {
                    yield return resultSelector(previous, e.Current);
                    previous = e.Current;
                }
            }
        }

        static IEnumerable<IGrouping<TKey, TSource>> GroupAdjacent<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector)
        {
            // ReSharper disable once IntroduceOptionalParameters.Local
            return GroupAdjacent(source, keySelector, null);
        }

        static IEnumerable<IGrouping<TKey, TSource>> GroupAdjacent<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey> comparer)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (keySelector == null) throw new ArgumentNullException("keySelector");

            return GroupAdjacent(source, keySelector, e => e, comparer);
        }

        static IEnumerable<IGrouping<TKey, TElement>> GroupAdjacent<TSource, TKey, TElement>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TSource, TElement> elementSelector)
        {
            // ReSharper disable once IntroduceOptionalParameters.Local
            return GroupAdjacent(source, keySelector, elementSelector, null);
        }

        static IEnumerable<IGrouping<TKey, TElement>> GroupAdjacent<TSource, TKey, TElement>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TSource, TElement> elementSelector,
            IEqualityComparer<TKey> comparer)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (keySelector == null) throw new ArgumentNullException("keySelector");
            if (elementSelector == null) throw new ArgumentNullException("elementSelector");

            return GroupAdjacentImpl(source, keySelector, elementSelector,
                                     comparer ?? EqualityComparer<TKey>.Default);
        }

        static IEnumerable<IGrouping<TKey, TElement>> GroupAdjacentImpl<TSource, TKey, TElement>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TSource, TElement> elementSelector,
            IEqualityComparer<TKey> comparer)
        {
            Debug.Assert(source != null);
            Debug.Assert(keySelector != null);
            Debug.Assert(elementSelector != null);
            Debug.Assert(comparer != null);

            using (var iterator = source.Select(item => KeyValuePair.Create(keySelector(item), elementSelector(item)))
                                        .GetEnumerator())
            {
                var group = default(TKey);
                var members = (List<TElement>) null;

                while (iterator.MoveNext())
                {
                    var item = iterator.Current;
                    if (members != null && comparer.Equals(group, item.Key))
                    {
                        members.Add(item.Value);
                    }
                    else
                    {
                        if (members != null)
                            yield return CreateGroupAdjacentGrouping(group, members);
                        group = item.Key;
                        members = new List<TElement> { item.Value };
                    }
                }

                if (members != null)
                    yield return CreateGroupAdjacentGrouping(group, members);
            }
        }

        static Grouping<TKey, TElement> CreateGroupAdjacentGrouping<TKey, TElement>(TKey key, IList<TElement> members)
        {
            Debug.Assert(members != null);
            return new Grouping<TKey, TElement>(key, members.IsReadOnly ? members : new ReadOnlyCollection<TElement>(members));
        }

        sealed class Grouping<TKey, TElement> : IGrouping<TKey, TElement>
        {
            readonly IEnumerable<TElement> _members;

            public Grouping(TKey key, IEnumerable<TElement> members)
            {
                Debug.Assert(members != null);
                Key = key;
                _members = members;
            }

            public TKey Key { get; private set; }

            public IEnumerator<TElement> GetEnumerator() { return _members.GetEnumerator(); }
            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        }

        #endregion
    }    
}

#if GINI_DYNAMIC

namespace Gini
{
    #region Imports

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Dynamic;
    using System.Globalization;
    using System.Linq;

    #endregion

    static partial class Ini
    {
        public static dynamic ParseObject(string ini)
        {
            return ParseObject(ini, null);
        }

        public static dynamic ParseObject(string ini, IEqualityComparer<string> comparer)
        {
            comparer = comparer ?? StringComparer.OrdinalIgnoreCase;
            var config = new Dictionary<string, Config<string>>(comparer);
            foreach (var section in from g in Parse(ini).GroupBy(e => e.Key ?? string.Empty, comparer)
                                    select KeyValuePair.Create(g.Key, g.SelectMany(e => e)))
            {
                var settings = new Dictionary<string, string>(comparer);
                foreach (var setting in section.Value)
                    settings[setting.Key] = setting.Value;
                config[section.Key] = new Config<string>(settings);
            }
            return new Config<Config<string>>(config);
        }

        public static dynamic ParseFlatObject(string ini, Func<string, string, string> keyMerger)
        {
            return ParseFlatObject(ini, keyMerger, null);
        }

        public static dynamic ParseFlatObject(string ini, Func<string, string, string> keyMerger, IEqualityComparer<string> comparer)
        {
            if (keyMerger == null) throw new ArgumentNullException("keyMerger");
            return new Config<string>(ParseFlatHash(ini, keyMerger, comparer));
        }

        sealed class Config<T> : DynamicObject
        {
            readonly IDictionary<string, T> _entries;

            public Config(IDictionary<string, T> entries)
            {
                Debug.Assert(entries != null);
                _entries = entries;
            }

            public override bool TryGetMember(GetMemberBinder binder, out object result)
            {
                if (binder == null) throw new ArgumentNullException("binder");
                result = Find(binder.Name);
                return true;
            }

            public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
            {
                if (indexes == null) throw new ArgumentNullException("indexes");
                if (indexes.Length != 1) throw new ArgumentException("Too many indexes supplied.");
                var index = indexes[0];
                result = Find(index == null ? null : Convert.ToString(index, CultureInfo.InvariantCulture));
                return true;
            }

            object Find(string name)
            {
                T value;
                return _entries.TryGetValue(name, out value) ? value : default(T);
            }
        }
    }
}

#endif
