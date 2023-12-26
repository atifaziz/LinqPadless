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
    #region Imports

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using MoreLinq;
    using static DeferDisposable;

    #endregion

    partial class Program
    {
        static int BundleCommand(IEnumerable<string> args) =>
            BundleArguments.CreateParser().Run(CommandName.Bundle, args, BundleCommand);

        static int BundleCommand(BundleArguments args)
        {
            var force = args.OptForce;
            var bundlePath = args.OptOut;
            var log = args.OptVerbose ? Console.Error : null;

            var queryPath = args.ArgFile;
            Debug.Assert(queryPath != null);

            var query = LinqPadQuery.Load(Path.GetFullPath(queryPath));
            if (query.ValidateSupported() is Exception e)
                throw e;

            var logbundlePath = bundlePath == null;
            bundlePath ??= Path.ChangeExtension(query.FilePath, ".zip");

            if (!force && File.Exists(bundlePath))
                throw new Exception("Target bundle file already exists: " + bundlePath);

            File.Delete(bundlePath);

            var tempZipFilePath = Path.GetRandomFileName();
            using var _ = Defer(tempZipFilePath, File.Delete);
            using var zip = ZipFile.Open(tempZipFilePath, ZipArchiveMode.Create);

            var queryFileName = Path.GetFileName(query.FilePath);

            var epEntry = zip.CreateEntry(Path.GetFileName("lpless-bundle.ini"));
            epEntry.LastWriteTime = File.GetLastWriteTime(query.FilePath);
            using (var stream = epEntry.Open())
            using (var writer = new StreamWriter(stream, Utf8.BomlessEncoding))
                writer.WriteLine("entrypoint=" + queryFileName);

            var loads = new List<(string Name, string SourceFilePath)>();

            var mainEntry = zip.CreateEntry(queryFileName);
            mainEntry.LastWriteTime = File.GetLastWriteTime(query.FilePath);
            using (var stream = mainEntry.Open())
            using (var writer = new StreamWriter(stream, Utf8.BomlessEncoding))
            {
                var eomLineNumber = LinqPad.GetEndOfMetaLineNumber(new FileInfo(queryPath));
                foreach (var line in File.ReadLines(queryPath).Take(eomLineNumber))
                    writer.WriteLine(line);

                var loadNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    mainEntry.Name
                };

                foreach (var (line, load) in
                    query.Code.Lines()
                              .Index(1)
                              .LeftJoin(query.Loads, e => e.Key, e => e.LineNumber,
                                        line => (line.Value, default),
                                        (line, load) => (line.Value, load)))
                {
                    if (load != null)
                    {
                        var fileName = Path.GetFileName(load.Path);
                        var originalName = Path.GetFileNameWithoutExtension(fileName);
                        var extension = Path.GetExtension(fileName);

                        for (var counter = 1; !loadNameSet.Add(fileName); counter++)
                            fileName = originalName + counter.ToString(CultureInfo.InvariantCulture) + extension;

                        loads.Add((fileName, load.Path));

                        writer.WriteLine($@"#load "".\{fileName}"" // {line}");
                    }
                    else
                    {
                        writer.WriteLine(line);
                    }
                }
            }

            if (loads.Count == 0)
                Console.Error.WriteLine("Warning! No load directives found. Bundle is redundant.");

            foreach (var (name, path) in loads)
            {
                log?.WriteLine("+ " + path);
                var entry = zip.CreateEntry(name);
                entry.LastWriteTime = File.GetLastWriteTime(path);
                using var output = entry.Open();
                using var input = File.OpenRead(path);
                input.CopyTo(output);
                output.Flush();
            }

            zip.Dispose();
            File.Move(tempZipFilePath, bundlePath);

            if (logbundlePath)
                Console.Error.WriteLine(bundlePath);

            return 0;
        }
    }
}
