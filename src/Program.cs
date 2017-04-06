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
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Xml.Linq;
    using ByteSizeLib;
    using Mannex;
    using Mannex.IO;
    using NDesk.Options;
    using NuGet;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis;
    using global::NuGet.Frameworks;
    using global::NuGet.Versioning;
    using MoreLinq;

    #endregion

    static partial class Program
    {
        static partial void Wain(IEnumerable<string> args)
        {
            const string csx = "csx";
            const string exe = "exe";

            var verbose = false;
            var help = false;
            var recurse = false;
            var force = false;
            var watching = false;
            var incremental = false;
            var extraPackageList = new List<PackageReference>();
            var extraImportList = new List<string>();
            var target = (string) null;
            var targetFramework = NuGetFramework.Parse(AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName);

            var options = new OptionSet
            {
                { "?|help|h"      , "prints out the options", _ => help = true },
                { "verbose|v"     , "enable additional output", _ => verbose = true },
                { "d|debug"       , "debug break", _ => Debugger.Launch() },
                { "r|recurse"     , "include sub-directories", _ => recurse = true },
                { "f|force"       , "force continue on errors", _ => force = true },
                { "w|watch"       , "watch for changes and re-compile outdated", _ => watching = true },
                { "i|incremental" , "compile outdated scripts only", _ => incremental = true },
                { "ref|reference=", "extra NuGet reference", v => { if (!string.IsNullOrEmpty(v)) extraPackageList.Add(ParseExtraPackageReference(v)); } },
                { "imp|import="   , "extra import", v => { extraImportList.Add(v); } },
                { "t|target="     , csx + " = C# script (default); " + exe + " = executable (experimental)", v => target = v },
                { "fx="           , $"target framework; default: {targetFramework}", v => targetFramework = NuGetFramework.Parse(v) },
            };

            var tail = options.Parse(args.TakeWhile(arg => arg != "--"));

            if (verbose)
                Trace.Listeners.Add(new ConsoleTraceListener(useErrorStream: true));

            if (help || tail.Count == 0)
            {
                Help(options);
                return;
            }

            var generator =
                csx.Equals(target, StringComparison.OrdinalIgnoreCase)
                ? GenerateCsx
                : exe.Equals(target, StringComparison.OrdinalIgnoreCase)
                ? (Generator) GenerateExecutable
                : throw new Exception("Target is invalid or missing. Supported targets are: "
                                      + string.Join(", ", csx, exe));

            extraImportList.RemoveAll(string.IsNullOrEmpty);

            // TODO Allow package source to be specified via args

            var queries = GetQueries(tail, recurse);

            // TODO Allow packages directory to be specified via args

            const string packagesDirName = "packages";

            var compiler = Compiler(NuGetClient.CreateDefaultFactory(), packagesDirName, extraPackageList, extraImportList,
                                    targetFramework, generator,
                                    watching || incremental, force, verbose);

            if (watching)
            {
                if (tail.Count > 1)
                {
                    // TODO Support multiple watch roots

                    throw new NotSupportedException(
                        "Watch mode does not support multiple file specifications. " +
                        "Use a single wildcard specification instead instead to watch and re-compile several queries.");
                }

                var tokens = SplitDirFileSpec(tail.First()).Fold((dp, fs) =>
                (
                    dirPath : dp ?? Environment.CurrentDirectory,
                    fileSpec: fs
                ));

                using (var cts = new CancellationTokenSource())
                {
                    Console.CancelKeyPress += (_, e) =>
                    {
                        // TODO Re-consider proper cancellation

                        Console.WriteLine("Aborting...");
                        // ReSharper disable once AccessToDisposedClosure
                        cts.Cancel();
                        e.Cancel = true;
                    };

                    var changes =
                        FileMonitor.GetFolderChanges(
                            tokens.dirPath, tokens.fileSpec,
                            recurse,
                            NotifyFilters.FileName
                                | NotifyFilters.LastWrite,
                            WatcherChangeTypes.Created
                                | WatcherChangeTypes.Changed
                                | WatcherChangeTypes.Renamed,
                            cts.Token);

                    foreach (var e in from cs in changes.Throttle(TimeSpan.FromSeconds(2))
                                      select cs.Length)
                    {
                        Console.WriteLine($"{e} change(s) detected. Re-compiling...");

                        var count = 0;
                        var compiledCount = 0;
                        // ReSharper disable once LoopCanBeConvertedToQuery
                        // ReSharper disable once LoopCanBePartlyConvertedToQuery
                        // ReSharper disable once PossibleMultipleEnumeration
                        foreach (var query in queries)
                        {
                            // TODO Re-try on potential file locking issues

                            var compiled = compiler(query);
                            count++;
                            compiledCount += compiled ? 1 : 0;
                        }

                        if (count > 1)
                            Console.WriteLine($"Re-compiled {compiledCount:N0} of {count:N0} queries.");
                    }
                }
            }
            else
            {
                foreach (var query in queries)
                    compiler(query);
            }
        }

        static readonly char[] Wildchars = { '*', '?' };
        static readonly char[] PathSeparators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        static (string dirPath, string fileName) SplitDirFileSpec(string spec)
        {
            var i = spec.LastIndexOfAny(PathSeparators);
            // TODO handle rooted cases
            return i >= 0
                 ? (spec.Substring(0, i + 1), spec.Substring(i + 1))
                 : (null, spec);
        }

        static IEnumerable<string> GetQueries(IEnumerable<string> tail,
                                              bool includeSubdirs)
        {
            var dirSearchOption = includeSubdirs
                                ? SearchOption.AllDirectories
                                : SearchOption.TopDirectoryOnly;
            return
                from spec in tail
                let tokens = SplitDirFileSpec(spec).Fold((dp, fs) =>
                (
                    dirPath : dp ?? Environment.CurrentDirectory,
                    fileSpec: fs
                ))
                let dirPath = tokens.dirPath ?? Environment.CurrentDirectory
                from e in
                    tokens.fileSpec.IndexOfAny(Wildchars) >= 0
                    ? from fi in new DirectoryInfo(dirPath).EnumerateFiles(tokens.fileSpec, dirSearchOption)
                      select new { File = fi, Searched = true }
                    : Directory.Exists(spec)
                    ? from fi in new DirectoryInfo(spec).EnumerateFiles("*.linq", dirSearchOption)
                      select new { File = fi, Searched = true }
                    : new[] { new { File = new FileInfo(spec), Searched = false } }
                where !e.Searched
                      || (!e.File.Name.StartsWith(".", StringComparison.Ordinal)
                          && 0 == (e.File.Attributes & (FileAttributes.Hidden | FileAttributes.System)))
                select e.File.FullName;
        }

        delegate void Generator(string queryFilePath,
            // ReSharper disable once UnusedParameter.Local
            string packagesPath, QueryLanguage queryKind, string source,
            IEnumerable<string> imports, IEnumerable<(string path, IInstalledPackage sourcePackage)> references,
            IndentingLineWriter writer);

        static Func<string, bool> Compiler(Func<DirectoryInfo, NuGetClient> nugetFactory,
            string packagesPath,
            IEnumerable<PackageReference> extraPackages,
            IEnumerable<string> extraImports,
            NuGetFramework targetFramework,
            Generator generator,
            bool unlessUpToDate = false, bool force = false, bool verbose = false)
        {
            var writer = IndentingLineWriter.Create(Console.Out);

            return queryFilePath =>
            {
                try
                {
                    var scriptFile = new FileInfo(Path.ChangeExtension(queryFilePath, ".csx"));
                    if (unlessUpToDate && scriptFile.Exists && scriptFile.LastWriteTime > File.GetLastWriteTime(queryFilePath))
                    {
                        if (verbose)
                        {
                            writer.WriteLine($"{queryFilePath}");
                            writer.Indent().WriteLine("Skipping compilation because target appears up to date.");
                        }
                        return false;
                    }

                    var packagesDir = new DirectoryInfo(Path.Combine(// ReSharper disable once AssignNullToNotNullAttribute
                                                                     Path.GetDirectoryName(queryFilePath),
                                                                     packagesPath));

                    writer.WriteLine($"{queryFilePath}");

                    var info = Compile(queryFilePath, nugetFactory(packagesDir),
                                       extraPackages, extraImports,
                                       targetFramework,
                                       verbose, writer.Indent());

                    generator(queryFilePath, packagesDir.FullName,
                              info.queryKind, info.source, info.namespaces,
                              info.references, writer.Indent());

                    return true;
                }
                catch (Exception e)
                {
                    if (!force)
                        throw;
                    writer.Indent().WriteLines($"WARNING! {e.Message}");
                    if (verbose)
                        writer.Indent().Indent().WriteLines(e.ToString());
                    return false;
                }
            };
        }

        static (QueryLanguage queryKind, string source, IEnumerable<string> namespaces, IEnumerable<(string path, IInstalledPackage sourcePackage)> references)
            Compile(string queryFilePath, NuGetClient nuget,
            IEnumerable<PackageReference> extraPackageReferences,
            IEnumerable<string> extraImports,
            NuGetFramework targetFramework,
            bool verbose, IndentingLineWriter writer)
        {
            var eomLineNumber = LinqPad.GetEndOfMetaLineNumber(queryFilePath);
            var lines = File.ReadLines(queryFilePath);

            var xml = string.Join(Environment.NewLine,
                          // ReSharper disable once PossibleMultipleEnumeration
                          lines.Take(eomLineNumber));

            var query = XElement.Parse(xml);

            if (verbose)
                writer.Write(query);

            QueryLanguage queryKind;
            if (!Enum.TryParse((string) query.Attribute("Kind"), true, out queryKind)
                || (queryKind != QueryLanguage.Statements
                    && queryKind != QueryLanguage.Expression
                    && queryKind != QueryLanguage.Program))
            {
                throw new NotSupportedException("Only LINQPad " +
                    "C# Statements and Expression queries are fully supported " +
                    "and C# Program queries partially in this version.");
            }

            var nrs =
                from nrsq in new[]
                {
                    from nr in query.Elements("NuGetReference")
                    let v = (string) nr.Attribute("Version")
                    select new PackageReference((string)nr,
                                                string.IsNullOrEmpty(v) ? null : NuGetVersion.Parse(v),
                                                (bool?)nr.Attribute("Prerelease") ?? false),
                    extraPackageReferences,
                }
                from nr in nrsq
                select new
                {
                    nr.Id,
                    nr.Version,
                    nr.IsPrereleaseAllowed,
                    Title = string.Join(" ", Seq.Return(nr.Id,
                                                        nr.Version?.ToString(),
                                                        nr.IsPrereleaseAllowed ? "(pre-release)" : null)
                                                .Filter()),
                };

            nrs = nrs.ToArray();

            if (verbose && nrs.Any())
            {
                writer.WriteLine($"Packages referenced ({nrs.Count():N0}):");
                writer.Indent().WriteLines(from nr in nrs select nr.Title);
            }

            writer.WriteLine($"Packages directory: {nuget.PackagesPath}");

            LogHandlerSet CreateLogHandlers(IndentingLineWriter w) =>
                verbose
                ? new LogHandlerSet(info : message => w.WriteLine("[INF] " + message),
                                    warn : message => w.WriteLine("[WRN] " + message),
                                    error: message => w.WriteLine("[ERR] " + message),
                                    debug: message => w.WriteLine("[DBG] " + message))
                : LogHandlerSet.Null;

            nuget.LogHandlers = CreateLogHandlers(writer);
            var logSwap = Swapper(() => nuget.LogHandlers, v => nuget.LogHandlers = v);

            writer.WriteLine($"Packages target: {targetFramework}");

            var resolutionList = new List<(string assemblyPath, IInstalledPackage package)>();

            foreach (var nr in nrs)
            {
                var version = nr.Version;
                if (version == null)
                {
                    writer.WriteLine("Querying latest version of " + nr.Id + (nr.IsPrereleaseAllowed ? " (pre-release)" : null));
                    var vqw = writer.Indent();
                    using (logSwap(CreateLogHandlers(vqw)))
                    {
                        version = nuget.GetLatestVersionAsync(nr.Id, targetFramework, nr.IsPrereleaseAllowed).Result;

                        if (version == null)
                            throw new Exception("Package not found: " + nr.Title);

                        vqw.WriteLine("Using version " + version);
                    }
                }

                var pkg = nuget.FindInstalledPackage(nr.Id, version, nr.IsPrereleaseAllowed)
                          ?? nuget.InstallPackageAsync(nr.Id, version, nr.IsPrereleaseAllowed, targetFramework).Result;

                writer.WriteLine("Resolving references...");
                resolutionList.AddRange(
                    from r in nuget.GetReferencesTree(pkg, targetFramework, writer.Indent())
                    select (assemblyPath: r.reference, package: r.package));
            }

            var packagesPathWithTrailer = nuget.PackagesPath + Path.DirectorySeparatorChar;

            var resolution =
                resolutionList
                    .DistinctBy(r => (r.package.Id, r.package.Version, r.assemblyPath))
                    .Select(r => new
                    {
                        r.package,
                        AssemblyPath = r.assemblyPath != null
                            ? MakeRelativePath(queryFilePath, packagesPathWithTrailer)
                            + MakeRelativePath(packagesPathWithTrailer, r.assemblyPath)
                            : null,
                    })
                    .Partition(r => r.AssemblyPath == null, (ok, nok) => new
                    {
                        ResolvedReferences    = ok,
                        ReferencelessPackages = from r in nok
                                                select r.package.ToString(),
                    });

            resolution.ReferencelessPackages.StartIter(e =>
            {
                writer.WriteLine($"Warning! Packages with no references for {targetFramework}:");
                writer.Indent().WriteLines(e.ResumeFromCurrent());
            });

            var references = resolution.ResolvedReferences.ToArray();

            references.Select(r => r.AssemblyPath).StartIter(e =>
            {
                writer.WriteLine($"Resolved references ({references.Length:N0}):");
                writer.Indent().WriteLines(e.ResumeFromCurrent());
            });

            return (queryKind,
                    // ReSharper disable once PossibleMultipleEnumeration
                    string.Join(Environment.NewLine, lines.Skip(eomLineNumber)),
                    LinqPad.DefaultNamespaces
                            .Concat(from ns in query.Elements("Namespace")
                                    select (string)ns)
                            .Concat(extraImports),
                    LinqPad.DefaultReferences.Select(r => (r, default(IInstalledPackage)))
                            .Concat(from r in query.Elements("Reference")
                                    select new
                                    {
                                        Relative = (string) r.Attribute("Relative"),
                                        Path     = ((string) r).Trim(),
                                    }
                                    into r
                                    where r.Path.Length > 0
                                    select r.Relative?.Length > 0
                                         ? r.Relative // prefer
                                         : ResolveReferencePath(r.Path)
                                    into r
                                    select (r, default(IInstalledPackage)))
                            .Concat(from r in references
                                    select (r.AssemblyPath, r.package)));
        }

        static string ResolveReferencePath(string path)
        {
            if (path.Length == 0 || path[0] != '<')
                return path;
            var endIndex = path.IndexOf('>');
            if (endIndex < 0)
                return path;
            var token = path.Substring(1, endIndex - 1);
            string basePath;
            if (!DirPathByToken.TryGetValue(token, out basePath))
                throw new Exception($"Unknown directory token \"{token}\" in reference \"{path}\".");
            return Path.Combine(basePath, path.Substring(endIndex + 1).TrimStart(PathSeparators));
        }

        static Func<T, IDisposable> Swapper<T>(Func<T> getter, Action<T> setter) => value =>
        {
            var old = getter();
            setter(value);
            return new DelegatingDisposable(() => setter(old));
        };

        static Dictionary<string, string> _dirPathByToken;

        public static Dictionary<string, string> DirPathByToken =>
            _dirPathByToken ?? (_dirPathByToken = ResolvedDirTokens().ToDictionary(StringComparer.OrdinalIgnoreCase));

        static IEnumerable<(string token, string path)> ResolvedDirTokens()
        {
            yield return ("RuntimeDirectory", RuntimeEnvironment.GetRuntimeDirectory());
            yield return ("ProgramFiles"    , Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            yield return ("ProgramFilesX86" , Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            yield return ("MyDocuments"     , Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        }

        static bool IsMainAsync(string source) =>
            CSharpSyntaxTree
                .ParseText(source).GetRoot()
                .DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Any(md => "Main" == md.Identifier.Text
                            && md.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)));

        static void GenerateCsx(string queryFilePath,
            string packagesPath, QueryLanguage queryKind, string source,
            IEnumerable<string> imports, IEnumerable<(string path, IInstalledPackage sourcePackage)> references,
            IndentingLineWriter writer)
        {
            var body = queryKind == QueryLanguage.Expression
                     ? string.Join(Environment.NewLine, "System.Console.WriteLine(", source, ");")
                     : queryKind == QueryLanguage.Program
                     ? source + Environment.NewLine
                              + (IsMainAsync(source) ? "await " : null)
                              + "Main();"
                     : source;

            var rs = references.ToArray();

            File.WriteAllLines(Path.ChangeExtension(queryFilePath, ".csx"),
                from lines in new[]
                {
                    from r in rs
                    select $"#r \"{r.path}\"",

                    Seq.Return(string.Empty),

                    from ns in imports
                    select $"using {ns};",

                    Seq.Return(body, string.Empty),
                }
                from line in lines
                select line);

            // TODO User-supplied csi.cmd

            GenerateBatch(LoadTextResource("csi.cmd"), queryFilePath, packagesPath, rs);
        }

        static void GenerateBatch(string cmdTemplate,
                                  string queryFilePath, string packagesPath,
                                  IEnumerable<(string path, IInstalledPackage sourcePackage)> references)
        {
            var queryDirPath = Path.GetFullPath(// ReSharper disable once AssignNullToNotNullAttribute
                                                Path.GetDirectoryName(queryFilePath));

            var pkgdir = MakeRelativePath(queryDirPath + Path.DirectorySeparatorChar,
                                          packagesPath + Path.DirectorySeparatorChar);

            var installs =
                from r in references
                where r.sourcePackage != null
                select $"if not exist \"{r.path}\" nuget install {r.sourcePackage.Id} -Version {r.sourcePackage.Version}{(r.sourcePackage.Version.IsPrerelease ? " -Prerelease" : null)} -OutputDirectory {pkgdir.TrimEnd(Path.DirectorySeparatorChar)} >&2 || goto :pkgerr";

            cmdTemplate = Regex.Replace(cmdTemplate, @"^ *(::|rem) *__PACKAGES__",
                                string.Join(Environment.NewLine, installs),
                                RegexOptions.CultureInvariant
                                | RegexOptions.IgnoreCase
                                | RegexOptions.Multiline);

            cmdTemplate = cmdTemplate.Replace("__LINQPADLESS__", VersionInfo.FileVersion);

            File.WriteAllText(Path.ChangeExtension(queryFilePath, ".cmd"), cmdTemplate);
        }

        static void GenerateExecutable(string queryFilePath,
            string packagesPath, QueryLanguage queryKind, string source,
            IEnumerable<string> imports, IEnumerable<(string path, IInstalledPackage sourcePackage)> references,
            IndentingLineWriter writer)
        {
            // TODO error handling in generated code

            var body =
                queryKind == QueryLanguage.Expression
                ? Seq.Return(
                        "static class UserQuery {",
                        "    static void Main() {",
                        "        System.Console.WriteLine(", source, ");",
                        "    }",
                        "}")
                : queryKind == QueryLanguage.Program
                ? Seq.Return(
                        "class UserQuery {",
                        "    static int Main(string[] args) {",
                        "        new UserQuery().Main()" + (IsMainAsync(source) ? ".Wait()" : null) + "; return 0;",
                        "    }",
                            source,
                        "}")
                : CSharpSyntaxTree.ParseText("void Main() {" + source + "}")
                                  .GetRoot()
                                  .DescendantNodes()
                                  .OfType<AwaitExpressionSyntax>()
                                  .Any()
                ? Seq.Return(
                        "class UserQuery {",
                        "    static int Main(string[] args) {",
                        "        new UserQuery().Main().Wait(); return 0;",
                        "    }",
                        "    async Task Main() {",
                                source,
                        "    }",
                        "}")
                : Seq.Return(
                        "class UserQuery {",
                        "    static int Main(string[] args) {",
                        "        new UserQuery().Main(); return 0;",
                        "    }",
                        "    void Main() {",
                                source,
                        "    }",
                        "}");

            var rs = references.ToArray();

            var csFilePath = Path.ChangeExtension(queryFilePath, ".cs");
            File.WriteAllLines(csFilePath,
                from lines in new[]
                {
                    from ns in imports.GroupBy(e => e, StringComparer.Ordinal)
                    select $"using {ns.First()};",

                    body,

                    Seq.Return(string.Empty),
                }
                from line in lines
                select line);

            var quoteOpt = QuoteOpt(' ');
            var args = Seq.Return(csFilePath).Concat(rs.Select(r => "/r:" + r.path))
                          .Select(quoteOpt);
            var argsLine = string.Join(" ", args);

            var workingDirPath = Path.GetDirectoryName(queryFilePath);

            var binDirPath = Path.GetDirectoryName(new Uri(Assembly.GetEntryAssembly().CodeBase).LocalPath);
            var compilersPackagePath = Path.Combine(binDirPath, "Microsoft.Net.Compilers");
            var cscPath = Path.Combine(compilersPackagePath, "tools", "csc.exe");

            if (!File.Exists(cscPath))
            {
                writer.WriteLine("Installing C# compiler using NuGet:");
                InstallCompilersPackage(Path.Combine(binDirPath, "nuget.exe"),
                                        compilersPackagePath, new Version(2, 0, 1),
                                        writer.Indent());
            }

            writer.WriteLine(quoteOpt(cscPath) + " " + argsLine);

            Spawn(cscPath, argsLine, string.IsNullOrEmpty(workingDirPath)
                                     ? Environment.CurrentDirectory
                                     : Path.GetFullPath(workingDirPath),
                  writer.Indent(),
                  exitCode => new Exception($"C# compiler finished with a non-zero exit code of {exitCode}."));

            var queryDirPath = Path.GetFullPath(// ReSharper disable once AssignNullToNotNullAttribute
                                                Path.GetDirectoryName(queryFilePath))
                                   .TrimEnd(PathSeparators)
                             + Path.DirectorySeparatorChar;

            var privatePaths =
                from r in rs
                select Path.Combine(queryDirPath, r.path) into r
                where File.Exists(r)
                select Path.GetDirectoryName(r) into d
                where !string.IsNullOrEmpty(d)
                select MakeRelativePath(queryDirPath, d + Path.DirectorySeparatorChar) into d
                where d.Length > 0
                   // TODO consider warning user in following case
                   && !Path.IsPathRooted(d)
                   && ".." != d.Split(PathSeparators, 2, StringSplitOptions.None)[0]
                group 1 by d into g
                select g.Key.TrimEnd(PathSeparators);

            privatePaths = privatePaths.ToArray();

            if (privatePaths.Any())
            {
                var asmv1 = XNamespace.Get("urn:schemas-microsoft-com:asm.v1");
                var config =
                    new XElement("configuration",
                        new XElement("runtime",
                            new XElement(asmv1 + "assemblyBinding",
                                new XElement(asmv1 + "probing",
                                    new XAttribute("privatePath", string.Join(";", privatePaths))))));

                File.WriteAllText(Path.ChangeExtension(queryFilePath, ".exe.config"), config.ToString());
            }

            // TODO User-supplied exe.cmd

            GenerateBatch(LoadTextResource("exe.cmd"), queryFilePath, packagesPath, rs);
        }

        static void InstallCompilersPackage(string nugetExePath,
                                            string compilersPackagePath, Version version,
                                            IndentingLineWriter writer)
        {
            if (!File.Exists(Path.GetDirectoryName(nugetExePath)))
            {
                var tempDownloadPath = Path.GetTempFileName();
                var nugetExeUrl = new Uri("https://dist.nuget.org/win-x86-commandline/v3.5.0/nuget.exe");
                writer.WriteLine("Downloading NuGet from " + nugetExeUrl);
                using (var wc = new WebClient())
                    wc.DownloadFile(nugetExeUrl, tempDownloadPath);
                writer.WriteLine(
                    $"Downloaded NuGet to {nugetExePath} ({ByteSize.FromBytes(new FileInfo(tempDownloadPath).Length)}).");
                Directory.CreateDirectory(Path.GetDirectoryName(nugetExePath));
                File.Delete(nugetExePath);
                File.Move(tempDownloadPath, nugetExePath);
            }

            const string compilersPackageId = "Microsoft.Net.Compilers";

            var compilersPackageBasePath = Path.GetDirectoryName(compilersPackagePath);
            Spawn(nugetExePath, $"install {compilersPackageId} -Version {version}", compilersPackageBasePath,
                  writer.Indent(),
                  exitCode => new Exception($"NuGet finished with a non-zero exit code of {exitCode}."));

            Directory.Move(Directory.EnumerateDirectories(Path.Combine(compilersPackageBasePath),
                           Path.GetFileName(compilersPackagePath + ".*")).FirstOrDefault()
                           ?? throw new DirectoryNotFoundException(compilersPackageId + " package installation path not found."),
                           compilersPackagePath);
        }

        static void Spawn(string path, string args, string workingDirPath, IndentingLineWriter writer,
                          Func<int, Exception> errorSelector)
        {
            using (var process = Process.Start(new ProcessStartInfo
            {
                CreateNoWindow         = true,
                UseShellExecute        = false,
                FileName               = path,
                Arguments              = args,
                RedirectStandardError  = true,
                RedirectStandardOutput = true,
                WorkingDirectory       = workingDirPath,
            }))
            {
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
        }

        static Func<string, string> QuoteOpt(params char[] chars) =>
            s => s?.IndexOfAny(chars) >= 0 ? "\"" + s + "\"" : s;

        static PackageReference ParseExtraPackageReference(string input)
        {
            // Syntax: ID [ "@" VERSION ] ["++"]
            // Examples:
            //   Foo                 => Latest release of Foo
            //   Foo@2.1             => Foo release 2.1
            //   Foo++               => Latest pre-release of Foo
            //   Foo@3.0++           => Foo 3.0 pre-release

            const string plusplus = "++";
            var prerelease = input.EndsWith(plusplus, StringComparison.Ordinal);
            if (prerelease)
                input = input.Substring(0, input.Length - plusplus.Length);
            return input.Split('@', (id, version) => new PackageReference(id, NuGetVersion.TryParse(version, out var v) ? v : null, prerelease));
        }

        static readonly Lazy<FileVersionInfo> CachedVersionInfo = Lazy.Create(() => FileVersionInfo.GetVersionInfo(new Uri(typeof(Program).Assembly.CodeBase).LocalPath));
        static FileVersionInfo VersionInfo => CachedVersionInfo.Value;

        static void Help(OptionSet options)
        {
            var name    = Lazy.Create(() => Path.GetFileName(VersionInfo.FileName));
            var opts    = Lazy.Create(() => options.WriteOptionDescriptionsReturningWriter(new StringWriter { NewLine = Environment.NewLine }).ToString());
            var logo    = Lazy.Create(() => new StringBuilder().AppendLine($"{VersionInfo.ProductName} (version {VersionInfo.FileVersion})")
                                                               .AppendLine(VersionInfo.LegalCopyright.Replace("\u00a9", "(C)"))
                                                               .ToString());

            using (var stream = GetManifestResourceStream("help.txt"))
            using (var reader = new StreamReader(stream))
            using (var e = reader.ReadLines())
            while (e.MoveNext())
            {
                var line = e.Current;
                line = Regex.Replace(line, @"\$([A-Z][A-Z_]*)\$", m =>
                {
                    switch (m.Groups[1].Value)
                    {
                        case "NAME": return name.Value;
                        case "LOGO": return logo.Value;
                        case "OPTIONS": return opts.Value;
                        default: return string.Empty;
                    }
                });

                if (line.Length > 0 && line[line.Length - 1] == '\n')
                    Console.Write(line);
                else
                    Console.WriteLine(line);
            }
        }

        static string LoadTextResource(string name, Encoding encoding = null) =>
            LoadTextResource(typeof(Program), name, encoding);

        static string LoadTextResource(Type type, string name, Encoding encoding = null)
        {
            using (var stream = GetManifestResourceStream(type, name))
            {
                Debug.Assert(stream != null);
                using (var reader = new StreamReader(stream, encoding ?? Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        static Stream GetManifestResourceStream(string name) =>
            GetManifestResourceStream(typeof(Program), name);

        static Stream GetManifestResourceStream(Type type, string name) =>
            type.Assembly.GetManifestResourceStream(type, name);

        /// <remarks>
        /// Credit http://stackoverflow.com/a/340454/6682
        /// </remarks>

        static string MakeRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath)) throw new ArgumentNullException(nameof(fromPath));
            if (string.IsNullOrEmpty(toPath)) throw new ArgumentNullException(nameof(toPath));

            var fromUri = new Uri(fromPath);
            var toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme)
                return toPath; // path can't be made relative.

            var relativeUri = fromUri.MakeRelativeUri(toUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            return toUri.Scheme == Uri.UriSchemeFile
                   && Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar
                 ? relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                 : relativePath;
        }

        enum QueryLanguage  // ReSharper disable UnusedMember.Local
        {                   // ReSharper disable InconsistentNaming
            Expression,
            Statements,
            Program,
            VBExpression,
            VBStatements,
            VBProgram,
            FSharpExpression,
            FSharpProgram,
            SQL,
            ESQL,
        }
        // ReSharper restore InconsistentNaming
        // ReSharper restore UnusedMember.Local
    }

    sealed class PackageReference
    {
        public string Id { get; }
        public NuGetVersion Version { get; }
        public bool HasVersion => Version != null;
        public bool IsPrereleaseAllowed { get; }

        public PackageReference(string id, NuGetVersion version, bool isPrereleaseAllowed)
        {
            Id = id;
            Version = version;
            IsPrereleaseAllowed = isPrereleaseAllowed;
        }
    }
}
