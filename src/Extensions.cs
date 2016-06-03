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

    static class TextWriteExtensions
    {
        public static void WriteLineIndented(this TextWriter writer, int level, string text) =>
            writer.WriteLine(new string(' ', level * 2) + text);
    }

    static class Seq
    {
        public static IEnumerable<T[]> Buffer<T>(this IEnumerable<T> source, TimeSpan timeout)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return BufferIterator(source, timeout);
        }

        sealed class Signal
        {
            public static readonly Signal Singleton = new Signal();
            Signal() { }
        }

        static IEnumerable<T[]> BufferIterator<T>(IEnumerable<T> source, TimeSpan timeout)
        {
            var bc = new BlockingCollection<Tuple<T, ExceptionDispatchInfo, Signal>>();

            using (var timer = new Timer(delegate
            {
                bc.Add(Tuple.Create(default(T), default(ExceptionDispatchInfo), Signal.Singleton));
            }, dueTime: Timeout.Infinite, period: Timeout.Infinite, state: null))
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

                    bc.CompleteAdding();
                });

                var pending = new List<T>();
                foreach (var e in bc.GetConsumingEnumerable())
                {
                    e.Item2?.Throw();

                    if (e.Item3 == Signal.Singleton)
                    {
                        var buffer = pending.ToArray();
                        pending.Clear();
                        yield return buffer;
                    }
                    else
                    {
                        pending.Add(e.Item1);
                    }
                }

                if (pending.Count > 0)
                    yield return pending.ToArray();
            }
        }
    }
}