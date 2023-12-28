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
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;
    using KeyValuePairs;
    using Mannex.IO;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using MoreLinq;
    using NuGet.Versioning;
    using Optuple;
    using Optuple.Collections;
    using Optuple.Linq;
    using Optuple.RegularExpressions;
    using static Minifier;
    using static OptionTag;
    using static Optuple.OptionModule;
    using MoreEnumerable = MoreLinq.MoreEnumerable;

    #endregion

    static partial class Program
    {
        static IEnumerable<string> GetDotnetExecutableSearchPaths(IEnumerable<string> searchPaths) =>
            from sp in searchPaths
            from ext in RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Seq.Return(".exe", ".cmd", ".bat")
                : Seq.Return(string.Empty)
            select Path.Join(sp, "dotnet" + ext);

        static string GlobalPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "lpless");

        static IEnumerable<string> GetSearchPaths(DirectoryInfo baseDir) =>
            baseDir
                .SelfAndParents()
                .Append(new DirectoryInfo(GlobalPath))
                .TakeUntil(d => File.Exists(Path.Combine(d.FullName, ".lplessroot")))
                .Select(d => Path.Combine(d.FullName, ".lpless"));

        static string GetCacheDirPath(IEnumerable<string> searchPaths) =>
            searchPaths.Select(d => Path.Combine(d, "cache")).FirstOrDefault(Directory.Exists)
            ?? Path.Combine(Path.GetTempPath(), "lpless", "cache");

        static int Wain(IReadOnlyList<string> args)
        {
            switch (args.FirstOrDefault() ?? string.Empty)
            {
                case CommandName.Cache  : return CacheCommand(args);
                case CommandName.Init   : return InitCommand(args);
                case CommandName.Bundle : return BundleCommand(args);
                case CommandName.Inspect: return InspectCommand(args);
                case CommandName.Help   : return HelpCommand(args);
                case CommandName.License:
                    Console.WriteLine(ThisAssembly.Resources.License.Text);
                    return 0;
                default:
                    var parser = ExecuteArguments.CreateParser();
                    return parser.Run(args,
                                      static args =>
                                          DefaultCommand(args.ArgFile, args.ArgArgs, args.OptTemplate, args.OptOutput,
                                                         uncached: args.OptBuild || args.OptOutput != null,
                                                         inspection: Inspection.None,
                                                         dontExecute: args.OptX, force: args.OptForce,
                                                         publishIdleTimeout: TimeSpanHms.Parse(args.OptIdleTimeout),
                                                         publishTimeout: TimeSpanHms.Parse(args.OptTimeout),
                                                         log: args.OptVerbose ? Console.Error : null));
            }
        }

        static class CommandName
        {
            public const string Cache   = "cache";
            public const string Init    = "init";
            public const string Bundle  = "bundle";
            public const string Inspect = "inspect";
            public const string Help    = "help";
            public const string License = "license";

            public static readonly ImmutableArray<string> All =
                ImmutableArray.Create(Cache, Init, Bundle, Inspect, Help, License);
        }

        static int HelpCommand(IEnumerable<string> args) =>
            HelpArguments.CreateParser().Run(args, HelpCommand);

        static int HelpCommand(HelpArguments args)
        {
            if (args.ArgCommand is { } command)
            {
                if (CommandName.All.IndexOf(command) < 0)
                    throw new Exception($"\"{command}\" is an invalid command. Must be one of: {CommandName.All.ToDelimitedString(", ")}");

                if (command == CommandName.Help)
                    Help();
                else
                    _ = Wain(new[] { command, "--help" });
            }
            else
            {
                Help();
            }

            return 0;

            static void Help() => Program.Help(HelpArguments.Help, Console.Out);
        }

        static int DefaultCommand(
            string queryPath,
            IEnumerable<string> args,
            string template,
            string outDirPath,
            Inspection inspection,
            bool uncached, bool dontExecute, bool force,
            TimeSpan publishIdleTimeout, TimeSpan publishTimeout,
            TextWriter log)
        {
            var query = LinqPadQuery.Load(Path.GetFullPath(queryPath));

            if (query.ValidateSupported() is Exception e)
                throw e;

#pragma warning disable IDE0010 // Add missing cases
            switch (inspection)
#pragma warning restore IDE0010 // Add missing cases
            {
                case Inspection.Meta:
                    Console.WriteLine(query.MetaElement);
                    return 0;
                case Inspection.Code:
                    Console.WriteLine(query.Code);
                    return 0;
                case Inspection.Kind:
                    Console.WriteLine(query.Language);
                    return 0;
                case Inspection.Namespaces:
                    foreach (var ns in query.Namespaces)
                        Console.WriteLine(ns);
                    return 0;
                case Inspection.RemovedNamespaces:
                    foreach (var ns in query.NamespaceRemovals)
                        Console.WriteLine(ns);
                    return 0;
                case Inspection.Loads:
                    foreach (var load in query.Loads)
                        Console.WriteLine(load.LoadPath);
                    return 0;
                case Inspection.Packages:
                    foreach (var pr in
                        from pr in query.PackageReferences
                        select pr.Id
                             + (pr.Version is { } v ? "=" + v : null)
                             + (pr.IsPrereleaseAllowed ? "!" : null))
                    {
                        Console.WriteLine(pr);
                    }
                    return 0;
            }

            if (query.Loads.FirstOrNone(r => r.LoadPath.Length == 0
                                          || !Path.IsPathRooted(r.LoadPath)
                                          && r.LoadPath[0] != '.') is (true, var r))
            {
                throw new NotSupportedException($"Unsupported path \"{r.LoadPath}\" in load directive on line {r.LineNumber}.");
            }

            if (template?.Length == 0)
                throw new Exception("Template name cannot be empty.");

            var templateOverride = template != null;
            if (!templateOverride)
            {
                template = (
                    from firstNonBlankLine in query.Code.Lines().SkipWhile(string.IsNullOrWhiteSpace).FirstOrNone()
                    from m in Regex.Match(firstNonBlankLine, @"(?<=//#?![\x20\t]*).+").ToOption()
                    select m.Value.Trim().Split2(' ', StringSplitOptions.RemoveEmptyEntries))
                    switch
                    {
                        (SomeT, var (t, _)) => t,
                        _ => "template"
                    };
            }

            var queryDir = new DirectoryInfo(Path.GetDirectoryName(query.FilePath));

            var searchPaths = GetSearchPaths(queryDir).ToArray();

            IReadOnlyCollection<(string Name, IStreamable Content)> templateFiles = (
                from templateProjectPath in
                    searchPaths
                        .Select(d => Path.Combine(d, "templates", template))
                        .If(log, (ss, log) => ss.Do(() => log.WriteLine("Template searches:"))
                                                .Do(s => log.WriteLine("- " + s)))
                        .FirstOrNone(Directory.Exists)
                select new DirectoryInfo(templateProjectPath) into dir
                select
                    dir.EnumerateFiles("*", new EnumerationOptions())
                       .Where(f => ".cs".Equals(f.Extension, StringComparison.OrdinalIgnoreCase)
                                || ".csproj".Equals(f.Extension, StringComparison.OrdinalIgnoreCase)
                                || "global.json".Equals(f.Name, StringComparison.OrdinalIgnoreCase))
                       .Select(f => (f.Name, Streamable.ReadFile(f.FullName)))
                       .ToArray())
                switch
                {
                    (SomeT, var tfs) when tfs.Length > 0 => tfs,
                    _ => throw new Exception("No template for running query.")
                };

            static string MinifyLinqPadQuery(string text)
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
                             .ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);

            var hashSource =
                MoreEnumerable
                    .From(() => new MemoryStream(Encoding.ASCII.GetBytes("3")))
                    .Concat(from rn in templateFiles.OrderBy(rn => rn.Name, StringComparer.OrdinalIgnoreCase)
                            select minifierByExtension.TryGetValue(Path.GetExtension(rn.Name), out var minifier)
                                 ? rn.Content.MapText(minifier)
                                 : rn.Content
                            into content
                            select content.Open())
                    .If(templateOverride, ss => ss.Concat(MoreEnumerable.From(() => new MemoryStream(Utf8.BomlessEncoding.GetBytes(template)))))
                    .Concat(from load in query.Loads
                            select Streamable.ReadFile(load.Path)
                                             .MapText(MinifyLinqPadQuery)
                                             .Open())
                    .Concat(MoreEnumerable.From(() => Streamable.ReadFile(query.FilePath)
                                                                .MapText(MinifyLinqPadQuery)
                                                                .Open()))
                    .ToStreamable();

            string hash;
            using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA1))
            using (var stream = hashSource.Open())
            {
                var stdout = inspection == Inspection.HashSource
                           ? Console.OpenStandardOutput()
                           : null;

                for (var buffer = new byte[4096]; ;)
                {
                    var length = stream.Read(buffer, 0, buffer.Length);
                    if (length == 0)
                        break;

                    var span = buffer.AsSpan(0, length);

                    // Normalize line endings by removing CR, assuming only LF remain

                    do
                    {
                        const byte nul = 0, cr  = (byte)'\r';
                        var si = span.IndexOfAny(cr, nul);

                        if (si < 0)
                        {
                            sha.AppendData(span);
                            stdout?.Write(span);
                            break;
                        }

                        if (span[si] == nul)
                            throw new NotSupportedException("Binary data is not yet supported.");

                        sha.AppendData(span[..si]);
                        stdout?.Write(span[..si]);

                        span = span[(si + 1)..];
                    }
                    while (span.Length > 0);
                }

                if (inspection == Inspection.HashSource)
                    return 0;

                hash = BitConverter.ToString(sha.GetHashAndReset())
                                   .Replace("-", string.Empty)
                                   .ToLowerInvariant();
            }

            if (inspection == Inspection.Hash)
            {
                Console.WriteLine(hash);
                return 0;
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

            rerun:

            var dotnetSearchPaths = GetDotnetExecutableSearchPaths(searchPaths);
            var dotnetPath =
                dotnetSearchPaths
                    .If(log, (ps, log) => ps.Do(() => log.WriteLine(".NET Core CLI Searches:"))
                                            .Do(p => log.WriteLine("- " + p)))
                    .FirstOrNone(File.Exists).Or("dotnet");

            {
                if (!force && Run() is { } exitCode)
                    return exitCode;
            }

            var buildMutex = new Mutex(initiallyOwned: true,
                                       @"Global\lpless:" + hash,
                                       out var isBuildMutexOwned);

            try
            {
                if (!isBuildMutexOwned)
                {
                    try
                    {
                        log.WriteLine("Detected competing executions and waiting for other(s) to finish...");
                        if (!buildMutex.WaitOne(TimeSpan.FromMinutes(1.5)))
                            throw new TimeoutException("Timed-out waiting for competing execution(s) to finish.");
                        log.WriteLine("...other is done; proceeding...");
                    }
                    catch (AbandonedMutexException)
                    {
                        log.WriteLine("...detected abandonment by other execution!");
                    }

                    if (!force)
                        goto rerun;
                }

                if (Compile(query, srcDirPath, tmpDirPath, templateFiles,
                            dotnetPath, publishIdleTimeout, publishTimeout,
                            inspection, log) is { } exitCode)
                {
                    return exitCode;
                }

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
            finally
            {
                buildMutex.ReleaseMutex();
                buildMutex.Dispose();
            }

            {
                return Run() is { } exitCode
                     ? exitCode
                     : throw new Exception("Internal error executing compilation.");
            }

            int? Run()
            {
                if (!Directory.Exists(binDirPath))
                    return null;

                const string runtimeconfigJsonSuffix = ".runtimeconfig.json";

                var binPath =
                    Directory.GetFiles(binDirPath, "*.json")
                             .FirstOrNone(p => p.EndsWith(runtimeconfigJsonSuffix, StringComparison.OrdinalIgnoreCase))
                             .Select(p => p[..^runtimeconfigJsonSuffix.Length] + ".dll")
                             .Match(p  => p,
                                    () => Directory.GetFiles(binDirPath, "*.exe")
                                                   .SingleOrDefault());

                if (binPath == null)
                    return null;

                var psi = new ProcessStartInfo { UseShellExecute = false };

                if (binPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    psi.FileName = dotnetPath;
                    psi.ArgumentList.Add(binPath);
                }
                else
                {
                    psi.FileName = binPath;
                }

                var env = psi.Environment;
                env.Add("LPLESS_BIN_PATH", Assembly.GetEntryAssembly().Location);
                env.Add("LPLESS_LINQ_FILE_PATH", queryPath);
                env.Add("LPLESS_LINQ_FILE_HASH", hash);

                args.ForEach(psi.ArgumentList.Add);

                string FormatCommandLine() =>
                    PasteArguments.Paste(psi.ArgumentList.Prepend(psi.FileName));

                if (dontExecute)
                {
                    Console.WriteLine(FormatCommandLine());
                    return 0;
                }

                log?.WriteLine(FormatCommandLine());

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

        static readonly ValueTuple Unit = default;

        static int? Compile(LinqPadQuery query,
            string srcDirPath, string binDirPath,
            IEnumerable<(string Name, IStreamable Content)> templateFiles,
            string dotnetPath, TimeSpan publishTimeout, TimeSpan publishIdleTimeout,
            Inspection inspection, TextWriter log)
        {
            return _(IndentingLineWriter.CreateUnlessNull(log));

            int? _(IndentingLineWriter log)
            {
                log?.WriteLines(from r in query.MetaElement.Elements("Reference")
                                select "Warning! Reference will be ignored: " + (string)r);

                using var httpClient = new HttpClient();

                NuGetVersion GetLatestPackageVersion(string id, bool isPrereleaseAllowed)
                {
                    var latestVersion = Program.GetLatestPackageVersion(id, isPrereleaseAllowed, url =>
                    {
                        log?.WriteLine(url.OriginalString);
                        return httpClient.GetStringAsync(url).GetAwaiter().GetResult();
                    });
                    log?.WriteLine($"{id} -> {latestVersion}");
                    return latestVersion;
                }

                var nrs =
                    from nr in
                        query.PackageReferences
                            .Select(r => new
                            {
                                r.Id,
                                Version = Option.From(r.HasVersion, r.Version),
                                r.IsPrereleaseAllowed,
                                Source = None<string>(),
                                Priority = 0,
                            })
                            .Concat(from lq in query.Loads
                                    from r in lq.PackageReferences
                                    select new
                                    {
                                        r.Id,
                                        Version = Option.From(r.HasVersion, r.Version),
                                        r.IsPrereleaseAllowed,
                                        Source = Some(lq.LoadPath),
                                        Priority = -1,
                                    })
                    select new
                    {
                        nr.Id,
                        nr.Version,
                        ActualVersion =
                            nr.Version.Match(
                                some: Lazy.Value,
                                none: () => Lazy.Create(() => GetLatestPackageVersion(nr.Id, nr.IsPrereleaseAllowed))),
                        nr.IsPrereleaseAllowed,
                        nr.Priority,
                        nr.Source,
                    };

                if (inspection == Inspection.ActualPackages)
                {
                    foreach (var nr in nrs)
                    {
                        Console.WriteLine(nr.Id
                                          + nr.Version.Map(v => "=" + v).OrDefault()
                                          + (nr.IsPrereleaseAllowed ? "!" : null)
                                          + nr.Source.Map(s => $" <{s}>").OrDefault());
                    }

                    return 0;
                }

                var allQueries =
                    query.Loads
                         .Where(q => q.Language is LinqPadQueryLanguage.Statements
                                                or LinqPadQueryLanguage.Expression)
                         .Select(q => new
                         {
                             q.GetQuery().Namespaces,
                             q.GetQuery().NamespaceRemovals,
                             Path = Some(q.LoadPath)
                         })
                         .Append(new
                         {
                             query.Namespaces,
                             query.NamespaceRemovals,
                             Path = None<string>()
                         })
                         .ToImmutableArray();

                var namespaces = ImmutableArray.CreateRange(
                    from nss in new[]
                    {
                        from q in allQueries
                        let nsrs = q.NamespaceRemovals.ToHashSet(StringComparer.Ordinal)
                        from ns in LinqPad.DefaultNamespaces
                        where !nsrs.Contains(ns)
                        select new
                        {
                            Name = ns,
                            IsDefaulted = true,
                            QueryPath = q.Path,
                        },
                        from q in allQueries
                        from ns in q.Namespaces
                        select new
                        {
                            Name = ns,
                            IsDefaulted = false,
                            QueryPath = q.Path,
                        },
                    }
                    from ns in nss
                    select ns);

                if (inspection == Inspection.ActualNamespaces)
                {
                    foreach (var ns in namespaces)
                    {
                        Console.WriteLine(ns.Name
                                        + (ns.IsDefaulted ? "*" : null)
                                        + ns.QueryPath.Map(p => $" <{p}>").OrDefault());
                    }

                    return 0;
                }

                var references =
                    from r in nrs
                    select (r.Priority, Reference: new PackageReference(r.Id, r.ActualVersion.Value, r.IsPrereleaseAllowed));

                GenerateExecutable(srcDirPath, binDirPath, query,
                                   from ns in namespaces select ns.Name,
                                   references, templateFiles,
                                   dotnetPath, publishIdleTimeout, publishTimeout,
                                   log);

                return null;
            }
        }

        static IEnumerable<(string Namespace, bool IsDefaulted)>
            ProcessNamespaceDirectives(IEnumerable<string> namespaces, IEnumerable<string> removals) =>
                LinqPad.DefaultNamespaces
                    .Except(removals, StringComparer.Ordinal)
                    .Select(ns => (ns, true))
                    .Concat(
                        from ns in namespaces
                        select ns.StartsWith("static ", StringComparison.Ordinal)
                             ? ns.Split2(' ').MapItems((_, t) => (Left: "static ", Right: t))
                             : ns.IndexOf('=') > 0
                             ? ns.Split2('=').MapItems((id, nst) => (Left: id + "=", Right: nst))
                             : (Left: null, Right: ns)
                        into ns
                        select (ns.Left + Regex.Replace(ns.Right, @"\s+", string.Empty), false))
                    .DistinctBy(((string Namespace, bool) e) => e.Namespace, StringComparer.Ordinal);

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
            LinqPadQuery query,
            IEnumerable<string> imports,
            IEnumerable<(int Priority, PackageReference Reference)> packages,
            IEnumerable<(string Name, IStreamable Content)> templateFiles,
            string dotnetPath, TimeSpan publishIdleTimeout, TimeSpan publishTimeout,
            IndentingLineWriter log)
        {
            // TODO error handling in generated code

            var workingDirPath = srcDirPath;

            if (Directory.Exists(workingDirPath))
            {
                try
                {
                    Directory.Delete(workingDirPath, true);
                }
                catch (DirectoryNotFoundException)
                {
                     // ignore in case of a race condition
                }
            }

            _ = Directory.CreateDirectory(workingDirPath);

            var ps = packages.ToArray();

            var resourceNames =
                templateFiles
                    .ToDictionary(e => e.Name,
                                  e => e.Content,
                                  StringComparer.OrdinalIgnoreCase);

            var projectDocument =
                XDocument.Parse(resourceNames.Single(e => e.Key.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).Value.ReadText());

            var packageIdSet =
                ps.Select(e => e.Reference.Id)
                  .ToHashSet(StringComparer.OrdinalIgnoreCase);

            projectDocument
                .Descendants("PackageReference")
                .Where(e => packageIdSet.Contains((string) e.Attribute("Include")))
                .Remove();

            projectDocument.Element("Project").Add(
                new XElement("ItemGroup",
                    from p in ps
                    group p by p.Reference.Id into g
                    select g.OrderByDescending(p => p.Priority)
                            .ThenByDescending(p => p.Reference.Version)
                            .First()
                            .Reference
                    into p
                    select
                        new XElement("PackageReference",
                            new XAttribute("Include", p.Id),
                            new XAttribute("Version", p.Version))));

            var queryName = Path.GetFileNameWithoutExtension(query.FilePath);

            using (var xw = XmlWriter.Create(Path.Combine(workingDirPath, queryName + ".csproj"), new XmlWriterSettings
            {
                Encoding           = Utf8.BomlessEncoding,
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
                    ProcessNamespaceDirectives(imports, query.NamespaceRemovals)
                        .Select(e => $"using {e.Namespace};")
                        .ToDelimitedString(eol));

            program =
                Detemplate(program, "generator", () =>
                {
                    return $"[assembly: System.CodeDom.Compiler.GeneratedCode({SyntaxFactory.Literal(ThisAssembly.Info.Product)}, {SyntaxFactory.Literal(ThisAssembly.Info.FileVersion)})]";
                });

            program =
                Detemplate(program, "path-string",
                    SyntaxFactory.Literal(query.FilePath).ToString());

            program =
                Detemplate(program, "source-string",
                    () => SyntaxFactory.Literal(query.Code).ToString());

            var loads =
                ImmutableArray.CreateRange(
                    from load in query.Loads.Index(1)
                    where load.Value.Language == LinqPadQueryLanguage.Program
                    select load.WithValue(ProgramQuery.Parse(load.Value.Code, load.Value.Path)));

            program = Hooks.Aggregate(program, (p, h) =>
                Detemplate(p, $"hook-{h.Name}",
                           Lazy.Create(() =>
                               loads.MapValue(h.Getter)
                                    .Choose(e => e.Value is { } md
                                               ? Some(FormattableString.Invariant($"q => q.{md.Identifier}{e.Key}"))
                                               : default)
                                    .ToDelimitedString(", "))));

            var noSymbols = Enumerable.Empty<string>();

            var (body, symbols)
                = query.Language == LinqPadQueryLanguage.Expression
                ? (GenerateExpressionProgram(query, program), noSymbols)
                : query.Language == LinqPadQueryLanguage.Program
                ? GenerateProgram(query, program)
                : (Detemplate(program, "statements", "#line 1" + eol + query.GetMergedCode()), noSymbols);

            var baseCompilationSymbol
                = "LINQPAD_"
#pragma warning disable IDE0072 // Add missing cases
                + query.Language switch
#pragma warning restore IDE0072 // Add missing cases
                  {
                      LinqPadQueryLanguage.Expression => "EXPRESSION",
                      LinqPadQueryLanguage.Program    => "PROGRAM",
                      LinqPadQueryLanguage.Statements => "STATEMENTS",
                      _ => throw new NotSupportedException()
                  };

            if (body is { } someBody)
            {
                File.WriteAllLines(csFilePath,
                    Seq.Return("#define LPLESS",
                               "#define LPLESS_TEMPLATE_V2",
                               "#define " + baseCompilationSymbol)
                       .Concat(from s in symbols
                               select $"#define {baseCompilationSymbol}_{s}")
                       .Append(someBody)
                       .Append(string.Empty));
            }

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

            var loadedSources =
                from load in query.Loads.Index(1)
                where load.Value.Language == LinqPadQueryLanguage.Program
                let pq = ProgramQuery.Parse(load.Value.GetQuery().FormatCodeWithLoadDirectivesCommented(), load.Value.Path)
                select
                    load.WithValue(
                        ProcessNamespaceDirectives(load.Value.Namespaces, load.Value.NamespaceRemovals)
                            .Select(e => $"using {e.Namespace};")
                            .Concat(new[]
                            {
                                "partial class UserQuery",
                                "{",
                                FullSourceWithLineDirective(
                                    pq.Others.Where(sn => sn is not MethodDeclarationSyntax md || md != pq.Main),
                                    e => e is MethodDeclarationSyntax md && (   md == pq.OnInit
                                                                             || md == pq.OnStart
                                                                             || md == pq.OnFinish
                                                                             || md == pq.Main)
                                       ? md.WithIdentifier(SyntaxFactory.Identifier(md.Identifier.ValueText + load.Key))
                                       : e),
                                "}",
                                FullSourceWithLineDirective(pq.Types),
                                FullSourceWithLineDirective(pq.Namespaces),
                            })
                            .Prepend("#define LPLESS"));

            foreach (var (n, lines) in loadedSources)
            {
                using var w = new StreamWriter(Path.Combine(srcDirPath, FormattableString.Invariant($"Load{n}.cs")), false, Utf8.BomlessEncoding);
                foreach (var line in lines)
                    w.WriteLine(line);
            }

            var quiet = log == null;

            var publishArgs =
                Seq.Return(Some("publish"),
                           quiet ? Some("-nologo") : default,
                           Some("-c"), Some("Release"),
                           Some($"-p:{nameof(LinqPadless)}={ThisAssembly.Info.FileVersion}"),
                           Some("-o"), Some(binDirPath))
                   .Choose(e => e)
                   .ToArray();

            log?.WriteLine(PasteArguments.Paste(publishArgs.Prepend(dotnetPath)));

            var publishLog = log?.Indent();

            var errored = false;
            List<string> pendingNonErrors = null;

            foreach (var (_, line) in
                Spawn(dotnetPath, publishArgs, workingDirPath,
                      StdOutputStreamKind.Output, StdOutputStreamKind.Error,
                      publishIdleTimeout, publishTimeout, killTimeout: TimeSpan.FromSeconds(15),
                      exitCode => new ApplicationException($"dotnet publish ended with a non-zero exit code of {exitCode}.")))
            {
                if (quiet
                    && Regex.Match(line, @"(?<=:\s*)(error|warning|info)(?=\s+(\w{1,2}[0-9]+)\s*:)").Value is { } ms
                    && ms.Length > 0)
                {
                    if (ms == "error")
                    {
                        errored = true;
                        if (pendingNonErrors is { } nonErrors)
                        {
                            pendingNonErrors = null;
                            foreach (var nonError in nonErrors)
                                Console.Error.WriteLine(nonError);
                        }
                        Console.Error.WriteLine(line);
                    }
                    else if (!errored)
                    {
                        pendingNonErrors ??= new List<string>();
                        pendingNonErrors.Add(line);
                    }
                }

                publishLog?.WriteLines(line);
            }
        }

        static readonly (string Name, Func<ProgramQuery, MethodDeclarationSyntax> Getter)[] Hooks =
        {
            ("init"  , ld => ld.OnInit  ),
            ("start" , ld => ld.OnStart ),
            ("finish", ld => ld.OnFinish),
        };

        static string GenerateExpressionProgram(LinqPadQuery query, string template)
        {
            var eol = Environment.NewLine;
            var code = query.FormatCodeWithLoadDirectivesCommented();

            var program = Detemplate(template, "expression", $"#line 1 \"{query.FilePath}\"{eol}{code}");

            var loads =
                ImmutableArray.CreateRange(
                    from load in query.Loads
                    where load.Language == LinqPadQueryLanguage.Program
                    select ProgramQuery.Parse(load.Code, load.Path));

            var printers =
                ImmutableArray.CreateRange(
                    from e in
                        loads.SelectMany(load => load.Others.OfType<MethodDeclarationSyntax>())
                             .Choose(md =>
                                 from a in md.AttributeLists
                                             .SelectMany(attrs => attrs.Attributes)
                                             .Where(attr =>
                                                 attr.Name.ToString() switch
                                                 {
                                                     "QueryExpressionPrinter" => true,
                                                     "QueryExpressionPrinterAttribute" => true,
                                                     _ => false
                                                 })
                                             .FirstOrNone()
                                 select (Method: md.Identifier.ValueText, Attribute: a))
                    group e.Attribute by e.Method
                    into g
                    select (Method: g.Key, Attribute: g.First()));

            switch (printers.Length)
            {
                case 0: break;
                case 1: program = Detemplate(program, "expression-printer", printers[0].Method); break;
                default: throw new Exception("Ambiguous expression printers: " +
                                             string.Join(", ",
                                                 from p in printers
                                                 select p.Attribute.SyntaxTree.GetMappedLineSpan(p.Attribute.Span) into loc
                                                 select $"{loc.Path}({loc.StartLinePosition.Line + 1},{loc.StartLinePosition.Character + 1})"));
            }

            return
                Hooks.Aggregate(program, (p, h) =>
                    Detemplate(p, "expression-hook-" + h.Name,
                               Lazy.Create(() =>
                                   loads.Select(h.Getter)
                                        .Index(1)
                                        .Choose(e => e.Value is { } md
                                                   ? Some(FormattableString.Invariant($"{md.Identifier}{e.Key}();"))
                                                   : default)
                                        .ToDelimitedString(eol))));
        }

        static string FullSourceWithLineDirective(IEnumerable<SyntaxNode> sns) =>
            FullSourceWithLineDirective(sns, sn => sn);

        static string FullSourceWithLineDirective<T>(IEnumerable<T> nns, Func<T, SyntaxNode> nf)
            where T : SyntaxNode =>
            nns.Select(e => "#line " +
                            e.GetLineNumber().ToString(CultureInfo.InvariantCulture)
                          + " \"" + e.SyntaxTree.FilePath + "\""
                          + Environment.NewLine
                          + nf(e).ToFullString())
               .Append(Environment.NewLine)
               .ToDelimitedString(string.Empty);

        static int GetLineNumber(this SyntaxNode node) =>
            node.SyntaxTree.GetLineSpan(node.FullSpan).StartLinePosition.Line + 1;

        static (string Source, IEnumerable<string> CompilationSymbols)
            GenerateProgram(LinqPadQuery query, string template)
        {
            var parts = ProgramQuery.Parse(query.FormatCodeWithLoadDirectivesCommented(), query.FilePath);

            var program =
                Detemplate(template, "program-namespaces",
                    FullSourceWithLineDirective(parts.Namespaces));

            program =
                Detemplate(program, "program-types",
                    FullSourceWithLineDirective(parts.Types));

            var main = parts.Main;

            var loadedStatements = Lazy.Create(() =>
                ((BlockSyntax)SyntaxFactory
                    .ParseStatement(Seq.Return("{", query.GetMergedCode(true), ";", "}")
                                       .ToDelimitedString(Environment.NewLine))).Statements);

            var newMain =
                query.Loads.Any(q => q.Language is LinqPadQueryLanguage.Expression
                                                or LinqPadQueryLanguage.Statements)
                ? main.ExpressionBody is { } arrow
                  ? main.WithExpressionBody(null).WithSemicolonToken(default)
                        .WithBody(SyntaxFactory.Block(loadedStatements.Value.Add(SyntaxFactory.ExpressionStatement(arrow.Expression))))
                  : main.WithBody(SyntaxFactory.Block(loadedStatements.Value.AddRange(
                      from stmt in main.Body.Statements.Index()
                      select stmt.Key == 0
                           ? stmt.Value.WithLeadingTrivia(
                                 SyntaxFactory.Trivia(
                                 SyntaxFactory.LineDirectiveTrivia(SyntaxFactory.Literal(stmt.Value.GetLineNumber()),
                                                                   SyntaxFactory.Literal($"\"{query.FilePath}\"", query.FilePath), true).NormalizeWhitespace()))
                           : stmt.Value)))
                : main;

            program =
                Detemplate(program, "program",
                    FullSourceWithLineDirective(parts.Others,
                        e =>  e == main
                           ? newMain.WithIdentifier(SyntaxFactory.Identifier("RunUserAuthoredQuery"))
                           : e is MethodDeclarationSyntax md && (e == parts.OnInit || e == parts.OnStart || e == parts.OnFinish)
                           ? md.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword)
                               .WithTrailingTrivia(SyntaxFactory.Whitespace(" ")))
                           : e));

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

            [ static ] ( void | int | Task | Task<int> ) Main([ string[] args ]) { }

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
                          .If(hasArgs , ss => ss.Append("ARGS"))
                          .If(isVoid  , ss => ss.Append("VOID"))
                          .If(isTask  , ss => ss.Append("TASK"))
                          .If(isAsync , ss => ss.Append("ASYNC"))
                          .If(isStatic, ss => ss.Append("STATIC")));
        }

        static string Detemplate(string template, string name, string replacement) =>
            Detemplate(template, name, Lazy.Value(replacement));

        static string Detemplate(string template, string name, Func<string> replacement) =>
            Detemplate(template, name, Lazy.Create(replacement));

        static string Detemplate(string template, string name, Lazy<string> replacement) =>
            Regex.Matches(template, @"
                     (?<= ^ | \r?\n )
                     [\x20\t]* // !? [\x20\t]* {% [\x20\t]*([a-z-]+)
                     (?: [\x20\t]* %}
                       | \s.*? // !? [\x20\t]* %}
                       )
                     [\x20\t]* (?=\r?\n)"
                     , RegexOptions.Singleline
                     | RegexOptions.IgnorePatternWhitespace)
                 .Aggregate((Index: 0, Text: string.Empty),
                            (s, m) =>
                                (m.Index + m.Length,
                                 s.Text + template[s.Index..m.Index]
                                        + (string.Equals(name, m.Groups[1].Value, StringComparison.OrdinalIgnoreCase)
                                           ? replacement.Value
                                           : m.Value)),
                            s => s.Text + template[s.Index..]);

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

            var (_, version) =
                from f in XDocument.Parse(xml).FindElement(atom + "feed")
                from e in f.Elements(atom + "entry").SingleOrNone()
                from p in e.FindElement(m + "properties")
                from v in p.FindElement(d + "Version")
                select NuGetVersion.Parse((string)v);

            return version ?? throw new Exception($"Unable to determine latest{(isPrereleaseAllowed ? " (pre-release)" : null)} version of package named \"{id}\".");
        }

        enum StdOutputStreamKind { Output, Error }

        static IEnumerable<(T, string)>
            Spawn<T>(string path, IEnumerable<string> args,
                     string workingDirPath, T outputTag, T errorTag,
                     TimeSpan idleTimeout, TimeSpan executionTimeout, TimeSpan killTimeout,
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

            var output = new BlockingCollection<(T, string)>();
            var tsLock = new object();
            var lastDataTimestamp = DateTime.MinValue;

            DataReceivedEventHandler OnStdDataReceived(T tag, TaskCompletionSource<DateTime> tcs) =>
                (_, e) =>
                {
                    var now = DateTime.Now;
                    lock (tsLock)
                    {
                        if (now > lastDataTimestamp)
                            lastDataTimestamp = now;
                    }

                    if (e.Data == null)
                        tcs.SetResult(now);
                    else
                        output.Add((tag, e.Data));
                };

            var tcsStdOut = new TaskCompletionSource<DateTime>();
            var tcsStdErr = new TaskCompletionSource<DateTime>();

            _ = Task.WhenAll(tcsStdOut.Task, tcsStdErr.Task)
                    .ContinueWith(_ => output.CompleteAdding());

            process.OutputDataReceived += OnStdDataReceived(outputTag, tcsStdOut);
            process.ErrorDataReceived  += OnStdDataReceived(errorTag , tcsStdErr);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var heartbeatCancellationTokenSource = new CancellationTokenSource();
            var heartbeatCancellationToken = heartbeatCancellationTokenSource.Token;
            using var timeoutCancellationTokenSource = new CancellationTokenSource();

            if (executionTimeout > TimeSpan.Zero)
                timeoutCancellationTokenSource.CancelAfter(executionTimeout);

            var isClinicallyDead = false;

            async Task Heartbeat()
            {
                var delay = idleTimeout;
                while (!heartbeatCancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(delay, heartbeatCancellationToken);

                    TimeSpan durationSinceLastData;
                    lock (tsLock) durationSinceLastData = DateTime.Now - lastDataTimestamp;

                    if (idleTimeout > TimeSpan.Zero && durationSinceLastData > idleTimeout)
                    {
                        isClinicallyDead = true;
                        timeoutCancellationTokenSource.Cancel();
                        break;
                    }

                    delay = idleTimeout - durationSinceLastData;
                }
            }

            var heartbeatTask = Heartbeat();

            using (var e = output.GetConsumingEnumerable(timeoutCancellationTokenSource.Token)
                                 .GetEnumerator())
            {
                while (true)
                {
                    try
                    {
                        if (!e.MoveNext())
                            break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    yield return e.Current;
                }
            }

            heartbeatCancellationTokenSource.Cancel();

            try
            {
                heartbeatTask.GetAwaiter().GetResult(); // await graceful shutdown
            }
            catch (OperationCanceledException) { } // expected so ignore
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            if (isClinicallyDead
                || !process.WaitForExit(executionTimeout > TimeSpan.Zero
                                        ? (int)executionTimeout.TotalMilliseconds
                                        : Timeout.Infinite))
            {
                try
                {
                    process.Kill();
                }
                catch (Win32Exception e) // If Kill call is made while the process is terminating,
                {                        // a Win32Exception is thrown for "Access Denied" (2).
                    Debug.WriteLine(e);
                }

                var error = $"Timeout expired waiting for process {process.Id} to {(isClinicallyDead ? "respond" : "exit")}.";

                // Killing of a process executes asynchronously so wait for the process to exit

                if (!process.WaitForExit(killTimeout > TimeSpan.Zero
                                         ? (int)killTimeout.TotalMilliseconds
                                         : Timeout.Infinite))
                {
                    error += " The process did not terminate on time on killing either.";
                }

                throw new TimeoutException(error);
            }

            var exitCode = process.ExitCode;
            if (exitCode != 0)
                throw errorSelector(exitCode);
        }
    }

    static class Utf8
    {
        public static readonly Encoding BomlessEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}
