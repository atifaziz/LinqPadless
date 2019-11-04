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
    using System.Linq;
    using System.IO;
    using System.Text;
    using System.Xml.Linq;
    using static MoreLinq.Extensions.PipeExtension;
    using static Optuple.OptionModule;

    static class StringExtensions
    {
        public static (string, string)
            Split2(this string input, char separator,
                   StringSplitOptions options = StringSplitOptions.None)
        {
            var tokens = input.Split(separator, 2, options);
            return tokens.Length switch
            {
                0 => default,
                1 => (tokens[0], default),
                _ => (tokens[0], tokens[1])
            };
        }

        public static StringBuilder AppendLines(this StringBuilder builder, IEnumerable<string> lines)
        {
            foreach (var line in lines)
                builder.AppendLine(line);
            return builder;
        }

        public static IEnumerable<string> Lines(this string input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return _(); IEnumerable<string> _()
            {
                using var reader = new StringReader(input);
                while (reader.ReadLine() is string line)
                    yield return line;
            }
        }
    }

    static class EnumerableExtensions
    {
        public static IEnumerable<T> Do<T>(this IEnumerable<T> source, Action<T> action) =>
            source.Pipe(action);

        public static IEnumerable<T> If<T, TArg>(this IEnumerable<T> source, TArg arg,
                                                 Func<IEnumerable<T>, TArg, IEnumerable<T>> then)
            where TArg : class
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (then == null) throw new ArgumentNullException(nameof(then));

            return arg != null ? then(source, arg) : source;
        }

        public static IEnumerable<T> If<T>(this IEnumerable<T> source, bool flag,
                                           Func<IEnumerable<T>, IEnumerable<T>> then)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (then == null) throw new ArgumentNullException(nameof(then));

            return flag ? then(source) : source;
        }

        public static IEnumerable<T> Do<T>(this IEnumerable<T> source, Action action)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return _(); IEnumerable<T> _()
            {
                using var e = source.GetEnumerator();
                action();
                while (e.MoveNext())
                    yield return e.Current;
            }
        }

        public static IEnumerable<TValue> Values<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source) =>
            from e in source select e.Value;
    }

    static class TupleExtensions
    {
        public static TResult MapItems<T1, T2, TResult>(this ValueTuple<T1, T2> tuple, Func<T1, T2, TResult> mapper) =>
            mapper(tuple.Item1, tuple.Item2);
    }

    static class XmlExtensions
    {
        public static (bool Found, XElement Element) FindElement(this XContainer element, XName name) =>
            element.Element(name) switch { null => default, var e => Some(e) };
    }

    static class OptionSetExtensions
    {
        public static T WriteOptionDescriptionsReturningWriter<T>(this Mono.Options.OptionSet options, T writer)
            where T : TextWriter
        {
            options.WriteOptionDescriptions(writer);
            return writer;
        }
    }
}
