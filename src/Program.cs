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
    using System.IO.Compression;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reactive.Disposables;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;
    using Choices;
    using Mannex.IO;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis;
    using NuGet.Versioning;
    using MoreEnumerable = MoreLinq.MoreEnumerable;
    using static MoreLinq.Extensions.AggregateExtension;
    using static MoreLinq.Extensions.IndexExtension;
    using static MoreLinq.Extensions.ChooseExtension;
    using static MoreLinq.Extensions.PartitionExtension;
    using static MoreLinq.Extensions.TakeUntilExtension;
    using static MoreLinq.Extensions.ToDelimitedStringExtension;
    using static MoreLinq.Extensions.FoldExtension;
    using static MoreLinq.Extensions.ToDictionaryExtension;
    using static MoreLinq.Extensions.TagFirstLastExtension;
    using static OptionModule;
    using Ix = System.Linq.EnumerableEx;
    using OptionSetArgumentParser = System.Func<System.Func<string, Mono.Options.OptionContext, bool>, string, Mono.Options.OptionContext, bool>;
    using static Minifier;

    #endregion

    static partial class Program
    {
        static IEnumerable<string> GetDotnetExecutableSearchPaths(IEnumerable<string> searchPaths) =>
            from sp in searchPaths
            from ext in RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Seq.Return(".exe", ".cmd", ".bat")
                : Seq.Return(string.Empty)
            select Path.Join(sp, "dotnet" + ext);

        static IEnumerable<string> GetSearchPaths(DirectoryInfo baseDir) =>
            baseDir
                .SelfAndParents()
                .TakeUntil(d => File.Exists(Path.Combine(d.FullName, ".lplessroot")))
                .Select(d => Path.Combine(d.FullName, ".lpless"));

        static string GetCacheDirPath(IEnumerable<string> searchPaths) =>
            searchPaths.Select(d => Path.Combine(d, "cache")).FirstOrDefault(Directory.Exists)
            ?? Path.Combine(Path.GetTempPath(), "lpless", "cache");

        static int Wain(IEnumerable<string> args)
        {
            var verbose = Ref.Create(false);
            var help = Ref.Create(false);
            var force = false;
            var dontExecute = false;
            var outDirPath = (string) null;
            var uncached = false;
            var template = (string) null;

            var options = new OptionSet(CreateStrictOptionSetArgumentParser())
            {
                Options.Help(help),
                Options.Verbose(verbose),
                Options.Debug,
                { "f|force"       , "force re-fresh/build", _ => force = true },
                { "x"             , "do not execute", _ => dontExecute = true },
                { "b|build"       , "build entirely to output directory; implies -f", _ => uncached = true },
                { "o|out|output=" , "output directory; implies -b and -f", v => outDirPath = v },
                { "t|template="   , "template", v => template = v },
            };

            var tail = options.Parse(args);

            if (verbose)
                Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));

            if (help || tail.Count == 0)
            {
                Help(options);
                return 0;
            }

            var command = tail.First();
            args = tail.Skip(1).TakeWhile(arg => arg != "--");

            return command switch
            {
                "cache" => CacheCommand(args),
                "init"  => InitCommand(args).GetAwaiter().GetResult(),
                _ => // ...
                    DefaultCommand(command, args, template, outDirPath,
                                   uncached: uncached || outDirPath != null,
                                   dontExecute: dontExecute,
                                   force: force, verbose: verbose)
            };
        }

        static int DefaultCommand(
            string queryPath,
            IEnumerable<string> args,
            string template,
            string outDirPath,
            bool uncached, bool dontExecute, bool force, bool verbose)
        {
            var query = LinqPadQuery.Load(Path.GetFullPath(queryPath));

            if (!query.IsLanguageSupported)
            {
                throw new NotSupportedException("Only LINQPad " +
                                                "C# Statements and Expression queries are fully supported " +
                                                "and C# Program queries partially in this version.");
            }

            if (template?.Length == 0)
                throw new Exception("Template name cannot be empty.");

            var whackBang
                = query.Code.Lines().SkipWhile(string.IsNullOrWhiteSpace).FirstOrDefault() is string firstNonBlankLine
                ? Regex.Match(firstNonBlankLine, @"(?<=//#?![\x20\t]*).+").Value.Trim()
                : default;

            var templateOverride = template != null;
            if (!templateOverride)
            {
                template = whackBang.Split2(' ', StringSplitOptions.RemoveEmptyEntries)
                                    .Fold((t, _) => t ?? "template");
            }

            var queryDir = new DirectoryInfo(Path.GetDirectoryName(query.FilePath));
            var searchPaths = GetSearchPaths(queryDir).ToArray();

            IReadOnlyCollection<(string Name, IStreamable Content)> templateFiles
                = searchPaths
                    .Select(d => Path.Combine(d, "templates", template))
                    .If(verbose, ss => ss.Do(() => Console.Error.WriteLine("Template searches:"))
                                         .WriteLine(Console.Error, s => "- " + s))
                    .FirstOrDefault(Directory.Exists) is string templateProjectPath
                ? Directory.GetFiles(templateProjectPath)
                           .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                                    || f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                                    || "global.json".Equals(Path.GetFileName(f), StringComparison.OrdinalIgnoreCase))
                           .Select(f => (Path.GetFileName(f), Streamable.Create(() => File.OpenRead(f))))
                           .ToArray()
                : default;

            if (templateFiles == null || templateFiles.Count == 0)
                throw new Exception("No template for running query.");

            string MinifyLinqPadQuery(string text)
            {
                var eomLineNumber = LinqPad.GetEndOfMetaLineNumber(text);
                return
                    text.Lines()
                        .Index(1)
                        .Partition(e => e.Key <= eomLineNumber, (xml, cs) => Seq.Return(xml, cs))
                        .Select(s => s.Values().ToDelimitedString(Environment.NewLine))
                        .Fold((xml, cs) => MinifyXml(xml) + "\n" + MinifyCSharp(cs));
            };

            var minifierTable = new (Func<string, string> Function, IEnumerable<string> Extension)[]
            {
                (MinifyJavaScript, Seq.Return(".json")),
                (MinifyCSharp    , Seq.Return(".cs")),
                (MinifyXml       , Seq.Return(".xml", ".csproj")),
            };

            var minifierByExtension =
                minifierTable.SelectMany(m => m.Extension, (m, ext) => KeyValuePair.Create(ext, m.Function))
                             .ToDictionary(StringComparer.OrdinalIgnoreCase);

            var hashSource =
                MoreEnumerable
                    .From(() => new MemoryStream(Encoding.ASCII.GetBytes("1.0")))
                    .Concat(from rn in templateFiles.OrderBy(rn => rn.Name, StringComparer.OrdinalIgnoreCase)
                            select minifierByExtension.TryGetValue(Path.GetExtension(rn.Name), out var minifier)
                                 ? rn.Content.MapText(minifier)
                                 : rn.Content
                            into content
                            select content.Open())
                    .Concat(Ix.If(() => templateOverride,
                                  MoreEnumerable.From(() => new MemoryStream(Utf8BomlessEncoding.GetBytes(template)))))
                    .Concat(MoreEnumerable.From(() => Streamable.ReadFile(query.FilePath)
                                                                .MapText(MinifyLinqPadQuery)
                                                                .Open()))
                    .ToStreamable();

            string hash;
            using (var sha = SHA1.Create())
            using (var stream = hashSource.Open())
            {
                hash = BitConverter.ToString(sha.ComputeHash(stream))
                                   .Replace("-", string.Empty)
                                   .ToLowerInvariant();
            }

            string cacheId, cacheBaseDirPath;

            if (uncached)
            {
                cacheId = ".";
                cacheBaseDirPath = outDirPath ??
                                   Path.Combine(queryDir.FullName, Path.GetFileNameWithoutExtension(query.FilePath));
                force = true;
            }
            else
            {
                cacheId = hash;
                cacheBaseDirPath = GetCacheDirPath(searchPaths);
            }

            var binDirPath = Path.Combine(cacheBaseDirPath, "bin", cacheId);
            var srcDirPath = Path.Combine(cacheBaseDirPath, "src", cacheId);

            if (!Path.IsPathFullyQualified(binDirPath))
                binDirPath = Path.GetFullPath(binDirPath);

            var tmpDirPath = uncached ? binDirPath : Path.Combine(cacheBaseDirPath, "bin", "!" + cacheId);

            var exporting = outDirPath != null && !uncached;
            if (exporting)
            {
                if (Directory.Exists(outDirPath))
                    throw new Exception("The output directory already exists.");

                force = true;
            }

            var dotnetSearchPaths = GetDotnetExecutableSearchPaths(searchPaths);
            var dotnetPath =
                dotnetSearchPaths
                    .If(verbose, ps => ps.Do(() => Console.Error.WriteLine(".NET Core CLI Searches:"))
                        .WriteLine(Console.Error, p => "- " + p))
                    .FirstOrDefault(File.Exists) ?? "dotnet";

            {
                if (!force && Run() is int exitCode)
                    return exitCode;
            }

            try
            {
                Compile(query, srcDirPath, tmpDirPath, templateFiles, dotnetPath, verbose);

                if (tmpDirPath != binDirPath)
                {
                    if (!exporting && Directory.Exists(binDirPath))
                        Directory.Delete(binDirPath, true);

                    Directory.Move(tmpDirPath, binDirPath);
                }
            }
            catch
            {
                try
                {
                    if (tmpDirPath != binDirPath)
                        Directory.Delete(tmpDirPath);
                }
                catch { /* ignore */}
                throw;
            }

            {
                return Run() is int exitCode
                     ? exitCode
                     : throw new Exception("Internal error executing compilation.");
            }

            int? Run()
            {
                if (!Directory.Exists(binDirPath))
                    return null;

                const string depsJsonSuffix = ".deps.json";

                var binPath =
                    Directory.GetFiles(binDirPath, "*.json")
                             .Where(p => p.EndsWith(depsJsonSuffix, StringComparison.OrdinalIgnoreCase))
                             .Select(p => p.Substring(0, p.Length - depsJsonSuffix.Length))
                             .FirstOrDefault(p => p != null) is string s ? s + ".dll" : null;

                if (binPath == null)
                    return null;

                var psi = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    FileName        = dotnetPath,
                    ArgumentList    = { binPath },
                };

                args.ForEach(psi.ArgumentList.Add);

                string FormatCommandLine() =>
                    PasteArguments.Paste(psi.ArgumentList.Prepend(psi.FileName));

                if (verbose && !dontExecute)
                    Console.Error.WriteLine(FormatCommandLine());

                if (dontExecute)
                {
                    Console.WriteLine(FormatCommandLine());
                    return 0;
                }

                const string runLogFileName = "runs.log";
                var runLogPath = Path.Combine(binDirPath, runLogFileName);
                var runLogLockTimeout = TimeSpan.FromSeconds(5);
                var runLogLockName = string.Join("-", "lpless", hash, runLogFileName);

                void LogRun(FormattableString str) =>
                    File.AppendAllLines(runLogPath, Seq.Return(FormattableString.Invariant(str)));

                using var runLogLock = ExternalLock.EnterLocal(runLogLockName, runLogLockTimeout);
                using var process = Process.Start(psi);
                Debug.Assert(process != null);

                var startTime = process.StartTime;
                LogRun($"> {startTime:o} {process.Id}");
                runLogLock.Dispose();

                process.WaitForExit();
                var endTime = DateTime.Now;

                if (ExternalLock.TryEnterLocal(runLogLockName, runLogLockTimeout, out var mutex))
                {
                    using var _ = mutex;
                    LogRun($"< {endTime:o} {startTime:o}/{process.Id} {process.ExitCode}");
                }

                return process.ExitCode;
            }
        }

        static class Options
        {
            public static Mono.Options.Option Help(Ref<bool> value) =>
                new ActionOption("?|help|h", "prints out the options", _ => value.Value = true);

            public static Mono.Options.Option Verbose(Ref<bool> value) =>
                new ActionOption("verbose|v", "enable additional output", _ => value.Value = true);

            public static readonly Mono.Options.Option Debug =
                new ActionOption("d|debug", "debug break", vs => Debugger.Launch());
        }

        static async Task<int> InitCommand(IEnumerable<string> args)
        {
            var help = Ref.Create(false);
            var verbose = Ref.Create(false);
            var force = false;
            var outputDirectoryPath = ".";
            var example = false;
            var specificVersion = (NuGetVersion)null;
            var feedDirPath = (string)null;
            var searchPrereleases = false;

            var options = new OptionSet(CreateStrictOptionSetArgumentParser())
            {
                Options.Help(help),
                Options.Verbose(verbose),
                Options.Debug,
                { "f|force", "force re-fresh/build", _ => force = true },
                { "o|output=", "output {DIRECTORY}", v => outputDirectoryPath = v },
                { "example", "add a simple example", _ => example = true },
                { "version=", "use package {VERSION}", v => specificVersion = NuGetVersion.Parse(v) },
                { "feed=", "use {PATH} as package feed", v => feedDirPath = v },
                { "pre|prerelease", "include pre-releases in searches", _ => searchPrereleases = true },
            };

            var tail = options.Parse(args);

            if (tail.Count > 1)
                throw new Exception("Invalid argument: " + tail[1]);

            var source = tail.FirstOrDefault() ?? "LinqPadless.Templates.Template";

            var log = verbose ? Console.Error : null;
            if (log != null)
                Trace.Listeners.Add(new TextWriterTraceListener(log));

            if (help)
            {
                Help(options);
                return 0;
            }

            Directory.CreateDirectory(outputDirectoryPath);

            if (!force && Directory.EnumerateFileSystemEntries(outputDirectoryPath).Any())
            {
                Console.Error.WriteLine("The output directory is not empty (use \"--force\" switch to override).");
                return 1;
            }

            using var disposables = new CompositeDisposable();

            var http = Lazy.Create(() =>
            {
                var client = CreateHttpClient();
                // ReSharper disable once AccessToDisposedClosure
                disposables.Add(client); // add to disposables if ever created.
                return client;
            });

            async Task Download(Uri fileUrl, string targetFilePath)
            {
                log?.WriteLine($"Downloading {fileUrl}...");

                using (var content = await http.Value.GetStreamAsync(fileUrl))
                using (var temp = File.Create(targetFilePath))
                    await content.CopyToAsync(temp);

                log?.WriteLine($"Downloaded {new FileInfo(targetFilePath).Length} bytes to: " + targetFilePath);
            }

            string zipPath;
            var shouldDeleteNonTemplateZip = false;

            if (Uri.TryCreate(source, UriKind.Absolute, out var url))
            {
                if (url.IsFile)
                {
                    zipPath = url.LocalPath;
                }
                else if (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
                {
                    zipPath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
                    disposables.Add(new TempFile(zipPath, LogTempFileDeletionError));
                    await Download(url, zipPath);
                }
                else
                {
                    throw new NotSupportedException("Unsupported URI scheme: " + url.Scheme);
                }
            }
            else if (Path.IsPathFullyQualified(source) || source.StartsWith(".", StringComparison.Ordinal))
            {
                zipPath = source;
            }
            else
            {
                var (id, versionString) = source.Split2('@');
                if (!Regex.IsMatch(id, @"^\w+([_.-]\w+)*$"))
                    throw new Exception("Invalid package name: " + (id.Length == 0 ? "(empty)" : id));

                var version
                    = !string.IsNullOrEmpty(versionString)
                    ? NuGetVersion.Parse(versionString)
                    : specificVersion;

                if (specificVersion != null && version != specificVersion)
                    throw new Exception($"Version specifications conflict ({version} <> {specificVersion}).");

                string localPackagePath = null;

                if (version is null)
                {
                    if (feedDirPath != null)
                    {
                        (_, version, localPackagePath) =
                            ListPackagesFromFileSystemFeed(feedDirPath)
                                .Where(p => (searchPrereleases || !p.Version.IsPrerelease)
                                         && string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
                                .DefaultIfEmpty()
                                .MaxBy(p => p.Version)
                                .First();
                    }
                    else
                    {
                        version = GetLatestPackageVersion(id, searchPrereleases, searchUrl =>
                        {
                            log?.WriteLine($"Searching latest version of {id}: {searchUrl.OriginalString}");
                            return http.Value.GetStringAsync(searchUrl).GetAwaiter().GetResult();
                        });
                    }

                    if (version is null)
                        throw new Exception($"Package {id} does not exist or has not been released.");

                    log?.WriteLine($"{id} -> {version}");
                }
                else if (feedDirPath != null)
                {
                    (_, _, localPackagePath) =
                        ListPackagesFromFileSystemFeed(feedDirPath)
                            .FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)
                                              && p.Version == version);

                    if (localPackagePath is null)
                        throw new Exception($"Package {id} does not exist or does not have version {version}.");
                }

                if (localPackagePath != null)
                {
                    zipPath = localPackagePath;
                    log?.WriteLine("Using local package: " + zipPath);
                }
                else
                {
                    var packageCacheDirPath = Path.Join(Path.GetTempPath(), "lpless", "nupkgs");
                    zipPath = Path.Join(packageCacheDirPath, $"{id}@{version}.nupkg");

                    if (File.Exists(zipPath) && ZipSignature.DoesFileHave(zipPath))
                    {
                        log?.WriteLine("Using cached package: " + zipPath);
                    }
                    else
                    {
                        var nupkgUrl = new Uri(new Uri("https://www.nuget.org/api/v2/package/"),
                                               Uri.EscapeDataString(id) + "/" + Uri.EscapeDataString(version.OriginalVersion));

                        var tempPath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
                        disposables.Add(new TempFile(tempPath, LogTempFileDeletionError));
                        await Download(nupkgUrl, tempPath);

                        log?.WriteLine("Caching downloaded package at: " + zipPath);

                        Directory.CreateDirectory(Path.GetDirectoryName(zipPath));
                        File.Move(tempPath, zipPath);
                        shouldDeleteNonTemplateZip = true;
                    }
                }
            }

            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var createdDirectories = new HashSet<string>();

                var templateFiles =
                    from e in zip.Entries
                    where e.Name.Length > 0
                    select new
                    {
                        ArchiveEntry = e,
                        SourcePath   = e.FullName,
                        TemplateFile = Regex.Match(e.FullName, @"(?<=([/\\]|^)\.lpless[/\\])templates[/\\]+.+").Value,
                    }
                    into e
                    where !string.IsNullOrEmpty(e.TemplateFile)
                    select new
                    {
                        e.ArchiveEntry, e.SourcePath,
                        TargetPath = Path.Join(outputDirectoryPath, ".lpless", e.TemplateFile),
                    };

                var count = 0;

                foreach (var e in templateFiles)
                {
                    log?.WriteLine($"Unarchived {e.TargetPath} ({e.SourcePath})");

                    var dir = Path.GetDirectoryName(e.TargetPath);
                    if (createdDirectories.Add(dir))
                        Directory.CreateDirectory(dir);

                    e.ArchiveEntry.ExtractToFile(e.TargetPath, true);
                    count++;
                }

                if (count == 0)
                {
                    if (shouldDeleteNonTemplateZip)
                        File.Delete(zipPath);
                    throw new Exception("No templates found in the supplied source.");
                }
            }

            var lplessRootFilePath = Path.Join(outputDirectoryPath, ".lplessroot");
            if (!File.Exists(lplessRootFilePath))
                File.Create(lplessRootFilePath).Close();

            if (example)
            {
                File.WriteAllLines(Path.Join(outputDirectoryPath, "Example.linq"), encoding: Utf8BomlessEncoding, contents: new []
                {
                    @"<Query Kind=""Expression"" />",
                    string.Empty,
                    @"""Hello, World!""",
                });
            }

            Directory.CreateDirectory(Path.Join(outputDirectoryPath, ".lpless", "cache"));

            return 0;

            void LogTempFileDeletionError(string tempFile, Exception error)
            {
                log?.WriteLine("Warning! Unable to delete temporary file: " + tempFile);
                log?.WriteLine(error.ToString()
                                    .Lines()
                                    .Select(s => "  " + s) // indent
                                    .ToDelimitedString(Environment.NewLine));
            }

            HttpClient CreateHttpClient() =>
                new HttpClient(new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.Deflate
                                           | DecompressionMethods.GZip
                });
        }

        static class ZipSignature
        {
            static ReadOnlySpan<byte> Signature => new byte[] { 80, 75, 3, 4 };

            public static bool DoesFileHave(string path)
            {
                Span<byte> buffer = stackalloc byte[4];
                int length;
                using (var stream = File.OpenRead(path))
                    length = stream.Read(buffer);
                return buffer.Slice(0, length).SequenceEqual(Signature);
            }
        }

        static IEnumerable<(string Id, NuGetVersion Version, string Path)>
            ListPackagesFromFileSystemFeed(string path)
        {
            var nupkgs =
                from fp in Directory.EnumerateFiles(path, "*.nupkg")
                where !fp.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase)
                   && ZipSignature.DoesFileHave(fp)
                select fp;

            foreach (var nupkg in nupkgs)
            {
                using var zip = ZipFile.OpenRead(nupkg);
                var file = zip.Entries.SingleOrDefault(e => e.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) && e.FullName == e.Name);
                if (file == null)
                    continue;

                XDocument nuspec;
                using (var stream = file.Open())
                {
                    try
                    {
                        nuspec = XDocument.Load(stream);
                    }
                    catch (XmlException)
                    {
                        continue;
                    }
                }

                var metadata =
                    nuspec.Elements().SingleOrDefault(e => e.Name.LocalName == "package")
                        ?.Elements().SingleOrDefault(e => e.Name.LocalName == "metadata");
                if (metadata == null)
                    continue;

                var id = ((string)metadata.Elements().SingleOrDefault(e => e.Name.LocalName == "id"))?.Trim();
                if (id == null)
                    continue;

                var versionString = ((string)metadata.Elements().SingleOrDefault(e => e.Name.LocalName == "version")).Trim();

                if (NuGetVersion.TryParse(versionString, out var version))
                    yield return (id, version, nupkg);
            }
        }

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
            if (tail.FirstOrDefault() is string arg)
                throw new Exception("Invalid argument: " + arg);

            if (verbose)
                Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));

            if (help)
            {
                Help(options);
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

        static readonly ValueTuple Unit = default;

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

                                var startTime = start.Time is string st ? ParseTime(st) : (DateTimeOffset?) null;
                                var endTime   = ParseTime(end.Time);
                                return (true, selector(endTime, startTime));
                            });

                    if (some)
                        yield return item;
                }

                DateTimeOffset ParseTime(string s) =>
                    DateTimeOffset.ParseExact(s, "o", CultureInfo.InvariantCulture);
            }
        }

        static void Compile(LinqPadQuery query,
            string srcDirPath, string binDirPath,
            IEnumerable<(string Name, IStreamable Content)> templateFiles,
            string dotnetPath, bool verbose = false)
        {
            var writer = IndentingLineWriter.Create(Console.Error);

            if (verbose)
            {
                writer.Write(query.MetaElement);

                foreach (var r in query.MetaElement.Elements("Reference"))
                    writer.WriteLine("Warning! Reference will be ignored: " + (string) r);
            }

            var wc = new WebClient();

            NuGetVersion GetLatestPackageVersion(string id, bool isPrereleaseAllowed)
            {
                var latestVersion = Program.GetLatestPackageVersion(id, isPrereleaseAllowed, url =>
                {
                    if (verbose)
                        writer.WriteLine(url.OriginalString);
                    return wc.DownloadString(url);
                });
                if (verbose)
                    writer.WriteLine($"{id} -> {latestVersion}");
                return latestVersion;
            }

            var nrs =
                from nr in query.PackageReferences
                select new
                {
                    nr.Id,
                    nr.Version,
                    ActualVersion = nr.HasVersion
                                  ? Lazy.Value(nr.Version)
                                  : Lazy.Create(() => GetLatestPackageVersion(nr.Id, nr.IsPrereleaseAllowed)),
                    nr.IsPrereleaseAllowed,
                    Title = Seq.Return(Some(nr.Id),
                                       Some(nr.Version?.ToString()),
                                       nr.IsPrereleaseAllowed ? Some("(pre-release)") : default)
                               .Choose(e => e)
                               .ToDelimitedString(" "),
                };

            nrs = nrs.ToArray();

            var namespaces =
                from nss in new[]
                {
                    from ns in LinqPad.DefaultNamespaces
                    select new
                    {
                        Name = ns,
                        IsDefaulted = true,
                    },
                    from ns in query.Namespaces
                    select new
                    {
                        Name = ns,
                        IsDefaulted = false,
                    },
                }
                from ns in nss
                select ns;

            namespaces = namespaces.ToArray();

            if (verbose)
            {
                if (nrs.Any())
                {
                    writer.WriteLine($"Packages ({nrs.Count():N0}):");
                    writer.WriteLines(from nr in nrs select "- " + nr.Title);
                }

                if (namespaces.Any())
                {
                    writer.WriteLine($"Imports ({query.Namespaces.Count:N0}):");
                    writer.WriteLines(from ns in namespaces select "- " + ns.Name + (ns.IsDefaulted ? "*" : null));
                }
            }

            var references =
                from r in nrs
                select new PackageReference(r.Id, r.ActualVersion.Value, r.IsPrereleaseAllowed);

            GenerateExecutable(srcDirPath, binDirPath, query,
                               from ns in namespaces select ns.Name,
                               references, templateFiles, dotnetPath, verbose, writer);
        }

        static readonly Encoding Utf8BomlessEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        [Flags]
        enum MainReturnTypeTraits
        {
            VoidTrait = 1,
            TaskTrait = 2,
            Int       = 0,
            Void      = VoidTrait,
            Task      = TaskTrait | VoidTrait,
            TaskOfInt = TaskTrait | Int,
        }

        static void GenerateExecutable(string srcDirPath, string binDirPath,
            LinqPadQuery query, IEnumerable<string> imports,
            IEnumerable<PackageReference> packages,
            IEnumerable<(string Name, IStreamable Content)> templateFiles,
            string dotnetPath, bool verbose, IndentingLineWriter writer)
        {
            // TODO error handling in generated code

            var workingDirPath = srcDirPath;
            if (!Directory.Exists(workingDirPath))
                Directory.CreateDirectory(workingDirPath);

            var ps = packages.ToArray();

            var resourceNames =
                templateFiles
                    .ToDictionary(e => e.Name,
                                  e => e.Content,
                                  StringComparer.OrdinalIgnoreCase);

            var projectDocument =
                XDocument.Parse(resourceNames.Single(e => e.Key.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).Value.ReadText());

            var packageIdSet =
                ps.Select(e => e.Id)
                  .ToHashSet(StringComparer.OrdinalIgnoreCase);

            projectDocument
                .Descendants("PackageReference")
                .Where(e => packageIdSet.Contains((string) e.Attribute("Include")))
                .Remove();

            projectDocument.Element("Project").Add(
                new XElement("ItemGroup",
                    from p in ps
                    select
                        new XElement("PackageReference",
                            new XAttribute("Include", p.Id),
                            new XAttribute("Version", p.Version))));

            var queryName = Path.GetFileNameWithoutExtension(query.FilePath);

            using (var xw = XmlWriter.Create(Path.Combine(workingDirPath, queryName + ".csproj"), new XmlWriterSettings
            {
                Encoding           = Utf8BomlessEncoding,
                Indent             = true,
                OmitXmlDeclaration = true,
            }))
            {
                projectDocument.WriteTo(xw);
            }

            const string mainFile = "Main.cs";
            var csFilePath = Path.Combine(workingDirPath, mainFile);
            File.Delete(csFilePath);

            var program = resourceNames[mainFile].ReadText();

            var eol = Environment.NewLine;

            program =
                Detemplate(program, "imports",
                    imports.GroupBy(e => e, StringComparer.Ordinal)
                           .Select(ns => $"using {ns.First()};")
                           .ToDelimitedString(eol));

            program =
                Detemplate(program, "generator", () =>
                {
                    var versionInfo = CachedVersionInfo.Value;
                    return $"[assembly: System.CodeDom.Compiler.GeneratedCode({SyntaxFactory.Literal(versionInfo.ProductName)}, {SyntaxFactory.Literal(versionInfo.FileVersion)})]";
                });

            var source = query.Code;


            program =
                Detemplate(program, "path-string",
                    SyntaxFactory.Literal(query.FilePath).ToString());

            program =
                Detemplate(program, "source-string",
                    () => SyntaxFactory.Literal(query.Code).ToString());

            var noSymbols = Enumerable.Empty<string>();

            var (body, symbols)
                = query.Language == LinqPadQueryLanguage.Expression
                ? (Detemplate(program, "expression", "#line 1" + eol + source), noSymbols)
                : query.Language == LinqPadQueryLanguage.Program
                ? GenerateProgram(source, program)
                : (Detemplate(program, "statements", "#line 1" + eol + source), noSymbols);

            var baseCompilationSymbol = "LINQPAD_" +
                ( query.Language == LinqPadQueryLanguage.Expression ? "EXPRESSION"
                : query.Language == LinqPadQueryLanguage.Program    ? "PROGRAM"
                : query.Language == LinqPadQueryLanguage.Statements ? "STATEMENTS"
                : throw new NotSupportedException()
                );

            if (body != null)
                File.WriteAllLines(csFilePath,
                    Seq.Return("#define LPLESS",
                               "#define LPLESS_TEMPLATE_V1",
                               "#define " + baseCompilationSymbol)
                       .Concat(from s in symbols
                               select $"#define {baseCompilationSymbol}_{s}")
                       .Append(body)
                       .Append(string.Empty));

            foreach (var (name, content) in
                from f in resourceNames
                where !string.Equals(mainFile, f.Key, StringComparison.OrdinalIgnoreCase)
                   && !f.Key.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                select f)
            {
                using var s = content.Open();
                using var w = File.Create(Path.Combine(srcDirPath, name));
                s.CopyTo(w);
            }

            var publishArgs =
                Seq.Return(Some("publish"),
                           !verbose ? Some("-nologo") : default,
                           Some("-v"), Some(verbose ? "m" : "q"),
                           !verbose ? Some("-clp:ErrorsOnly") : default,
                           Some("-c"), Some("Release"),
                           Some($"-p:{nameof(LinqPadless)}={CachedVersionInfo.Value.FileVersion}"),
                           Some("-o"), Some(binDirPath))
                   .Choose(e => e)
                   .ToArray();

            if (verbose)
                writer.WriteLine(PasteArguments.Paste(publishArgs.Prepend(dotnetPath)));

            Spawn(dotnetPath,
                  publishArgs,
                  workingDirPath, writer.Indent(),
                  exitCode => new Exception($"dotnet publish ended with a non-zero exit code of {exitCode}."));
        }

        static (string Source, IEnumerable<string> CompilationSymbols)
            GenerateProgram(string source, string template)
        {
            var eol = Environment.NewLine;

            var syntaxTree = CSharpSyntaxTree.ParseText(
                "class UserQuery {" + eol + source + eol + "}");

            var parts =
                syntaxTree
                    .GetRoot()
                    .ChildNodes()
                    .OfType<ClassDeclarationSyntax>().Single()
                    .ChildNodes()
                    .Select(n => new
                    {
                        LineNumber = syntaxTree.GetLineSpan(n.FullSpan).StartLinePosition.Line,
                        Node = n,
                    })
                    .Partition(e => e.Node is TypeDeclarationSyntax, (tds, etc) => new
                    {
                        Types  = from e in tds
                                 select new
                                 {
                                     e.LineNumber,
                                     Node = (TypeDeclarationSyntax) e.Node
                                 },
                        // ReSharper disable PossibleMultipleEnumeration
                        Main   = etc.Choose(e => e.Node is MethodDeclarationSyntax md && "Main" == md.Identifier.Text
                                               ? Some(new { e.LineNumber, Node = md })
                                               : default)
                                    .Single(),
                        Others = etc,
                        // ReSharper restore PossibleMultipleEnumeration
                    });

            string FullSourceWithLineDirective<T>(IEnumerable<T> nns, Func<T, int> lf, Func<T, SyntaxNode> nf) =>
                nns.Select(e => "#line " + lf(e).ToString(CultureInfo.InvariantCulture) + eol
                              + nf(e).ToFullString())
                   .Append(eol)
                   .ToDelimitedString(string.Empty);

            var program =
                Detemplate(template, "program-types",
                    FullSourceWithLineDirective(parts.Types, e => e.LineNumber, e => e.Node));

            var main = parts.Main.Node;

            program =
                Detemplate(program, "program",
                    FullSourceWithLineDirective(parts.Others,
                        e => e.LineNumber,
                        e => e.Node == main
                           ? main.WithIdentifier(SyntaxFactory.Identifier("RunUserAuthoredQuery"))
                           : e.Node));

            var isAsync = main.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));
            var isStatic = main.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));

            var t = main.ReturnType switch
            {
                IdentifierNameSyntax ins when "Task".Equals(ins.Identifier.Value) =>
                    MainReturnTypeTraits.Task,
                GenericNameSyntax gns when "Task".Equals(gns.Identifier.Value) =>
                    MainReturnTypeTraits.TaskOfInt,
                PredefinedTypeSyntax pdts when pdts.Keyword.IsKind(SyntaxKind.VoidKeyword) =>
                    MainReturnTypeTraits.Void,
                _ =>
                    MainReturnTypeTraits.Int
            };

            var isVoid = t.HasFlag(MainReturnTypeTraits.VoidTrait);
            var isTask = t.HasFlag(MainReturnTypeTraits.TaskTrait);

            var hasArgs = main.ParameterList.Parameters.Any();

            /*

            [ static ] ( void | int | Task | Task<int> ) Main([ string[] args ]) {}

            static void Main()                     | STATIC, VOID
            static int Main()                      | STATIC,
            static void Main(string[] args)        | STATIC, VOID, ARGS
            static int Main(string[] args)         | STATIC, ARGS
            static Task Main()                     | STATIC, VOID, TASK
            static Task<int> Main()                | STATIC, TASK
            static Task Main(string[] args)        | STATIC, VOID, TASK, ARGS
            static Task<int> Main(string[] args)   | STATIC, TASK, ARGS
            void Main()                            | VOID
            int Main()                             |
            void Main(string[] args)               | VOID, ARGS
            int Main(string[] args)                | ARGS
            Task Main()                            | VOID, TASK
            Task<int> Main()                       | TASK
            Task Main(string[] args)               | VOID, TASK, ARGS
            Task<int> Main(string[] args)          | TASK, ARGS

            */

            return (
                program,
                Enumerable.Empty<string>()
                          .Concat(Ix.If(() => hasArgs , Seq.Return("ARGS")))
                          .Concat(Ix.If(() => isVoid  , Seq.Return("VOID")))
                          .Concat(Ix.If(() => isTask  , Seq.Return("TASK")))
                          .Concat(Ix.If(() => isAsync , Seq.Return("ASYNC")))
                          .Concat(Ix.If(() => isStatic, Seq.Return("STATIC"))));
        }

        static string Detemplate(string template, string name, string replacement) =>
            Detemplate(template, name, Lazy.Value(replacement));

        static string Detemplate(string template, string name, Func<string> replacement) =>
            Detemplate(template, name, Lazy.Create(replacement));

        static string Detemplate(string template, string name, Lazy<string> replacement) =>
            Regex.Matches(template, @"
                     (?<= ^ | \r?\n )
                     [\x20\t]* // [\x20\t]* {% [\x20\t]*([a-z-]+)
                     (?: [\x20\t]* %}
                       | \s.*? // [\x20\t]* %}
                       )
                     [\x20\t]* (?=\r?\n)"
                     , RegexOptions.Singleline
                     | RegexOptions.IgnorePatternWhitespace)
                 .Aggregate((Index: 0, Text: string.Empty),
                            (s, m) =>
                                (m.Index + m.Length,
                                 s.Text + template.Substring(s.Index, m.Index - s.Index)
                                        + (string.Equals(name, m.Groups[1].Value, StringComparison.OrdinalIgnoreCase)
                                           ? replacement.Value
                                           : m.Value)),
                            s => s.Text + template.Substring(s.Index));

        static NuGetVersion GetLatestPackageVersion(string id, bool isPrereleaseAllowed, Func<Uri, string> downloader)
        {
            var atom = XNamespace.Get("http://www.w3.org/2005/Atom");
            var d    = XNamespace.Get("http://schemas.microsoft.com/ado/2007/08/dataservices");
            var m    = XNamespace.Get("http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");

            var url = "https://www.nuget.org/api/v2/Search()"
                    + "?$orderby=Id"
                    + "&searchTerm='PackageId:" + Uri.EscapeDataString(id) + "'"
                    + "&targetFramework=''"
                    + "&includePrerelease=" + (isPrereleaseAllowed ? "true" : "false")
                    + "&$skip=0&$top=1&semVerLevel=2.0.0";

            var xml = downloader(new Uri(url));

            var versions =
                from e in XDocument.Parse(xml)
                                   .Element(atom + "feed")?
                                   .Elements(atom + "entry")
                                   ?? throw Error()
                select NuGetVersion.Parse((string) e.Element(m + "properties")?
                                                    .Element(d + "Version")
                                                    ?? throw Error());

            return versions.SingleOrDefault();

            Exception Error() =>
                new Exception($"Unable to determine latest {(isPrereleaseAllowed ? " (pre-release)" : null)} version of package named \"{id}\".");
        }

        static void Spawn(string path, IEnumerable<string> args,
                          string workingDirPath, IndentingLineWriter writer,
                          Func<int, Exception> errorSelector)
        {
            var psi = new ProcessStartInfo
            {
                CreateNoWindow         = true,
                UseShellExecute        = false,
                FileName               = path,
                RedirectStandardError  = true,
                RedirectStandardOutput = true,
                WorkingDirectory       = workingDirPath,
            };

            args.ForEach(psi.ArgumentList.Add);

            using var process = Process.Start(psi);
            Debug.Assert(process != null);

            void OnStdDataReceived(object _, DataReceivedEventArgs e)
            {
                if (e.Data == null)
                    return;
                writer?.WriteLines(e.Data);
            }

            process.OutputDataReceived += OnStdDataReceived;
            process.ErrorDataReceived  += OnStdDataReceived;

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            var exitCode = process.ExitCode;
            if (exitCode != 0)
                throw errorSelector(exitCode);
        }

        static readonly Lazy<FileVersionInfo> CachedVersionInfo = Lazy.Create(() => FileVersionInfo.GetVersionInfo(new Uri(typeof(Program).Assembly.CodeBase).LocalPath));
        static FileVersionInfo VersionInfo => CachedVersionInfo.Value;

        static void Help(Mono.Options.OptionSet options)
        {
            var name    = Lazy.Create(() => Path.GetFileNameWithoutExtension(VersionInfo.FileName));
            var opts    = Lazy.Create(() => options.WriteOptionDescriptionsReturningWriter(new StringWriter { NewLine = Environment.NewLine }).ToString());
            var logo    = Lazy.Create(() => new StringBuilder().AppendLine($"{VersionInfo.ProductName} (version {VersionInfo.FileVersion})")
                                                               .AppendLines(Regex.Split(VersionInfo.LegalCopyright.Replace("\u00a9", "(C)"), @"\. *(?=(?:Portions +)?Copyright\b)")
                                                                                 .TagFirstLast((s, _, l) => l ? s : s + "."))
                                                               .ToString());

            using var stream = GetManifestResourceStream("help.txt");
            using var reader = new StreamReader(stream);
            using var e = reader.ReadLines();

            while (e.MoveNext())
            {
                var line = e.Current;
                line = Regex.Replace(line, @"\$([A-Z][A-Z_]*)\$", m => m.Groups[1].Value switch
                {
                    "NAME"    => name.Value,
                    "LOGO"    => logo.Value,
                    "OPTIONS" => opts.Value,
                    _ => string.Empty
                });

                if (line.Length > 0 && line[line.Length - 1] == '\n')
                    Console.Write(line);
                else
                    Console.WriteLine(line);
            }
        }

        static OptionSetArgumentParser CreateStrictOptionSetArgumentParser()
        {
            var hasTailStarted = false;
            return (impl, arg, context) =>
            {
                if (hasTailStarted) // once a tail, always a tail
                    return false;

                var isOption = impl(arg, context);
                if (!isOption)
                {
                    if (arg.Length > 0 && arg[0] == '-' && !hasTailStarted)
                        throw new Exception("Invalid argument: " + arg);
                    hasTailStarted = true;
                }

                return isOption;
            };
        }

        static string LoadTextResource(string name, Encoding encoding = null) =>
            LoadTextResource(typeof(Program), name, encoding);

        static string LoadTextResource(Type type, string name, Encoding encoding = null)
        {
            using var stream = type != null
                             ? GetManifestResourceStream(type, name)
                             : GetManifestResourceStream(null, name);
            Debug.Assert(stream != null);
            using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8);
            return reader.ReadToEnd();
        }

        static Stream GetManifestResourceStream(string name) =>
            GetManifestResourceStream(typeof(Program), name);

        static Stream GetManifestResourceStream(Type type, string name) =>
            type != null ? type.Assembly.GetManifestResourceStream(type, name)
                         : Assembly.GetCallingAssembly().GetManifestResourceStream(name);
    }

    static class Utf8
    {
        public static readonly Encoding BomlessEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}
