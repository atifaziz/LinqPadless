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
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using NDesk.Options;

    static class OptionSetExtensions
    {
        public static T WriteOptionDescriptionsReturningWriter<T>(this OptionSet options, T writer)
            where T : TextWriter
        {
            options.WriteOptionDescriptions(writer);
            return writer;
        }
    }

    static partial class Seq
    {
        public static IEnumerable<T> Return<T>(params T[] items) => items;

        public static IEnumerable<T> Filter<T>(this IEnumerable<T> source) =>
            from item in source where item != null select item;

        public static IEnumerable<T[]> Throttle<T>(this IEnumerable<T> source, TimeSpan timeout)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return ThrottleIterator(source, timeout,
                                    () => new List<T>(),
                                    (list, e) => { list.Add(e); return list; },
                                    list => list.ToArray());
        }

        public static IEnumerable<TState> Throttle<T, TState>(
            this IEnumerable<T> source, TimeSpan timeout,
            Func<TState> seeder, Func<TState, T, TState> accumulator)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (accumulator == null) throw new ArgumentNullException(nameof(accumulator));
            return ThrottleIterator(source, timeout, seeder, accumulator, s => s);
        }

        sealed class Signal
        {
            public static readonly Signal Singleton = new Signal();
            Signal() { }
        }

        static IEnumerable<TResult> ThrottleIterator<T, TState, TResult>(
            IEnumerable<T> source, TimeSpan timeout,
            Func<TState> seeder, Func<TState, T, TState> accumulator,
            Func<TState, TResult> resultSelector)
        {
            var bc = new BlockingCollection<Tuple<T, ExceptionDispatchInfo, Signal>>();

            using (var timer = new Timer(delegate
            {
                bc.TryAdd(Tuple.Create(default(T), default(ExceptionDispatchInfo), Signal.Singleton));
            },  dueTime: Timeout.Infinite, period: Timeout.Infinite, state: null))
            {
                Task.Run(() =>
                {
                    try
                    {
                        foreach (var e in source)
                        {
                            bc.Add(Tuple.Create(e, default(ExceptionDispatchInfo), default(Signal)));
                            // ReSharper disable once AccessToDisposedClosure
                            if (!timer.Change(timeout, Timeout.InfiniteTimeSpan))
                                throw new Exception("Internal error setting up sequence buffering timer.");
                        }
                    }
                    catch (Exception e)
                    {
                        bc.Add(Tuple.Create(default(T), ExceptionDispatchInfo.Capture(e), default(Signal)));
                    }
                    finally
                    {
                        // ReSharper disable once AccessToDisposedClosure
                        timer.Dispose();
                    }

                    bc.CompleteAdding();
                });

                var count = 0;
                var pending = seeder();
                foreach (var e in bc.GetConsumingEnumerable())
                {
                    e.Item2?.Throw();

                    if (e.Item3 == Signal.Singleton)
                    {
                        var buffer = resultSelector(pending);
                        count = 0; pending = seeder();
                        yield return buffer;
                    }
                    else
                    {
                        count++;
                        accumulator(pending, e.Item1);
                    }
                }

                if (count > 0)
                    yield return resultSelector(pending);
            }
        }

        public static void StartIter<T>(this IEnumerable<T> source, Action<IEnumerator<T>> action)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (action == null) throw new ArgumentNullException(nameof(action));
            using (var e = source.GetEnumerator())
                if (e.MoveNext())
                    action(e);
        }

        public static IEnumerator<T> ResumeFromCurrent<T>(this IEnumerator<T> remainder)
        {
            if (remainder == null) throw new ArgumentNullException(nameof(remainder));
            using (remainder)
            {
                yield return remainder.Current;
                while (remainder.MoveNext())
                    yield return remainder.Current;
            }
        }

        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, TValue>> source) =>
            ToDictionary(source, null);

        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, TValue>> source,
            IEqualityComparer<TKey> comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.ToDictionary(e => e.Key, e => e.Value, comparer);
        }
    }
}