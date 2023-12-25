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
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Choices;
    using MoreLinq;
    using Optuple.Collections;
    using static OptionTag;

    #endregion

    partial class Program
    {
        static int CacheCommand(IEnumerable<string> args)
        {
            var help = Ref.Create(false);
            var verbose = Ref.Create(false);

            var options = new OptionSet(CreateStrictOptionSetArgumentParser())
            {
                Options.Help(help),
                Options.Verbose(verbose),
                Options.Debug,
            };

            var tail = options.Parse(args);
            if (tail.FirstOrNone() is (SomeT, var arg))
                throw new Exception("Invalid argument: " + arg);

            if (verbose)
                Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));

            if (help)
            {
                Help(CommandName.Cache, Streamable.Create(ThisAssembly.Resources.Help.Cache.GetStream), options);
                return 0;
            }

            var baseDir = new DirectoryInfo(GetCacheDirPath(GetSearchPaths(new DirectoryInfo(Environment.CurrentDirectory))));
            var binDir = new DirectoryInfo(Path.Join(baseDir.FullName, "bin"));
            if (!binDir.Exists)
                return 0;

            foreach (var dir in
                from dir in binDir.EnumerateDirectories("*")
                where 0 == (dir.Attributes & (FileAttributes.Hidden | FileAttributes.System))
                   && dir.Name.Length == 40
                   && dir.Name[0] != '.'
                   && Regex.IsMatch(dir.Name, @"^[a-zA-Z0-9]{40}$")
                select dir)
            {
                var runLogPath = Path.Join(dir.FullName, "runs.log");

                var log
                    = File.Exists(runLogPath)
                    ? File.ReadLines(runLogPath)
                    : Enumerable.Empty<string>();

                var (count, lastRunTime) =
                    ParseRunLog(log, (lrt, _) => lrt)
                        .Aggregate(0, (a, _) => a + 1,
                                   DateTimeOffset.MinValue, (a, lrt) => lrt > a ? lrt : a,
                                   ValueTuple.Create);

                var output = count > 0
                           ? $"{dir.Name} (runs = {count}; last = {lastRunTime:yyyy'-'MM'-'ddTHH':'mm':'sszzz})"
                           : dir.Name;

                Console.WriteLine(output);
            }

            return 0;
        }

        static IEnumerable<T> ParseRunLog<T>(IEnumerable<string> lines,
            Func<DateTimeOffset, DateTimeOffset?, T> selector)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            return Iterable(); IEnumerable<T> Iterable()
            {
                var starts = new Dictionary<string, (string Time, string Pid)>(StringComparer.OrdinalIgnoreCase);

                foreach (var e in
                    from line in lines.NonBlanks()
                    select line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    into tokens
                    where tokens.Length >= 3
                    select Choice.If(tokens[0] == ">", () => tokens.Skip(1).Take(2), () =>
                           Choice.If(tokens[0] == "<", () => tokens.Skip(1).Take(3), () =>
                                     Unit)) into e
                    where e.Match(_ => true, _ => true, _ => false)
                    select e.Forbid3()
                            .Map1(fs => fs.Fold((ts, pid) => new { Time = ts, Pid = pid }))
                            .Map2(fs => fs.Fold((ts, id, ec) => new { Time = ts, Id = id, ExitCode = ec })))
                {
                    var (some, item) =
                        e.Match(
                            start =>
                            {
                                starts.Add(start.Time + start.Pid, (start.Time, start.Pid));
                                return default;
                            },
                            end =>
                            {
                                if (starts.TryGetValue(end.Id, out var start))
                                    starts.Remove(end.Id);

                                var startTime = start.Time is {} st ? ParseTime(st) : (DateTimeOffset?) null;
                                var endTime   = ParseTime(end.Time);
                                return (true, selector(endTime, startTime));
                            });

                    if (some)
                        yield return item;
                }

                static DateTimeOffset ParseTime(string s) =>
                    DateTimeOffset.ParseExact(s, "o", CultureInfo.InvariantCulture);
            }
        }
    }
}
