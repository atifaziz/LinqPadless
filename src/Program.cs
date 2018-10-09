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
    using System.Runtime.Versioning;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Xml;
    using System.Xml.Linq;
    using Mannex;
    using Mannex.IO;
    using Mono.Options;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis;
    using NuGet.Frameworks;
    using NuGet.Versioning;

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
            var target = csx;
            var targetFramework = NuGetFramework.Parse(Assembly.GetEntryAssembly().GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName);

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
                { "t|target="     , csx + " = C# script (default); " + exe + " = executable", v => target = v },
                { "fx="           , $"target framework; default: {targetFramework}", v => targetFramework = NuGetFramework.Parse(v) },
            };

            var tail = options.Parse(args.TakeWhile(arg => arg != "--"));

            if (verbose)
                Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));

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

            var compiler = Compiler(packagesDirName, extraPackageList, extraImportList,
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
            IEnumerable<string> imports, IEnumerable<(string path, PackageReference sourcePackage)> references,
            IndentingLineWriter writer);

        static Func<string, bool> Compiler(
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

                    var info = Compile(queryFilePath,
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

        static (QueryLanguage queryKind, string source, IEnumerable<string> namespaces, IEnumerable<(string path, PackageReference sourcePackage)> references)
            Compile(string queryFilePath,
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

            if (!Enum.TryParse((string) query.Attribute("Kind"), true, out QueryLanguage queryKind)
                || queryKind != QueryLanguage.Statements
                && queryKind != QueryLanguage.Expression
                && queryKind != QueryLanguage.Program)
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

            writer.WriteLine($"Packages target: {targetFramework}");

            var isNetCoreApp = ".NETCoreApp".Equals(targetFramework.Framework, StringComparison.OrdinalIgnoreCase);

            var defaultNamespaces
                = isNetCoreApp
                ? LinqPad.DefaultCoreNamespaces
                : LinqPad.DefaultNamespaces;

            var defaultReferences
                = isNetCoreApp
                ? Array.Empty<string>()
                : LinqPad.DefaultReferences;

            return (queryKind,
                    // ReSharper disable once PossibleMultipleEnumeration
                    string.Join(Environment.NewLine, lines.Skip(eomLineNumber)),
                    defaultNamespaces
                            .Concat(from ns in query.Elements("Namespace")
                                    select (string)ns)
                            .Concat(extraImports),
                    defaultReferences.Select(r => (r, default(PackageReference)))
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
                                    select (r, default(PackageReference)))
                            .Concat(from r in nrs
                                    select ((string) null, new PackageReference(r.Id, r.Version, r.IsPrereleaseAllowed))));
        }

        static string ResolveReferencePath(string path)
        {
            if (path.Length == 0 || path[0] != '<')
                return path;
            var endIndex = path.IndexOf('>');
            if (endIndex < 0)
                return path;
            var token = path.Substring(1, endIndex - 1);
            if (!DirPathByToken.TryGetValue(token, out var basePath))
                throw new Exception($"Unknown directory token \"{token}\" in reference \"{path}\".");
            return Path.Combine(basePath, path.Substring(endIndex + 1).TrimStart(PathSeparators));
        }

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
            IEnumerable<string> imports, IEnumerable<(string path, PackageReference sourcePackage)> references,
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
                    select r.sourcePackage into p
                    select new
                    {
                        p.Id,
                        Version = p.HasVersion
                                ? p.Version.ToString()
                                : GetLatestPackageVersion(p.Id, p.IsPrereleaseAllowed).ToString()
                    }
                    into p
                    select $"#r \"nuget: {p.Id}, {p.Version}\"",

                    Seq.Return(string.Empty),

                    from ns in imports
                    select $"using {ns};",

                    Seq.Return(body, string.Empty),
                }
                from line in lines
                select line);

            // TODO User-supplied csi.cmd

            foreach (var ext in new[] { ".cmd", ".sh" })
                GenerateBatch(LoadTextResource("dotnet-script" + ext), ext, queryFilePath, null, null);
        }

        static readonly Encoding Utf8BomlessEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        static void GenerateBatch(string template, string extension,
                                  string queryFilePath, string packagesPath,
                                  IEnumerable<(string path, PackageReference sourcePackage)> references)
        {
            template = template.Replace("__LINQPADLESS__", VersionInfo.FileVersion);
            File.WriteAllText(Path.ChangeExtension(queryFilePath, extension), template, Utf8BomlessEncoding);

            if (".sh".Equals(extension, StringComparison.OrdinalIgnoreCase)
                && (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)))
            {
                // TODO chmod +x on *nix
            }
        }

        static void GenerateExecutable(string queryFilePath,
            string packagesPath, QueryLanguage queryKind, string source,
            IEnumerable<string> imports, IEnumerable<(string path, PackageReference sourcePackage)> references,
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

            var queryName = Path.GetFileNameWithoutExtension(queryFilePath);
            var workingDirPath = Path.Combine(Path.GetDirectoryName(queryFilePath), queryName);
            if (!Directory.Exists(workingDirPath))
                Directory.CreateDirectory(workingDirPath);

            var projectRoot =
                new XElement("Project",
                    new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                    new XElement("PropertyGroup",
                        new XElement("OutputType", "Exe"),
                        // TODO Remove TargetFramework hard-coding
                        new XElement("TargetFramework", "netcoreapp2.0")),
                    new XElement("ItemGroup",
                        from r in rs
                        select
                            new XElement("PackageReference",
                                new XAttribute("Include", r.sourcePackage.Id),
                                r.sourcePackage.HasVersion
                                ? new XAttribute("Version", r.sourcePackage.Version)
                                : new XAttribute("Version", GetLatestPackageVersion(r.sourcePackage.Id, r.sourcePackage.IsPrereleaseAllowed)))));

            using (var xw = XmlWriter.Create(Path.Combine(workingDirPath, queryName + ".csproj"), new XmlWriterSettings
            {
                Encoding           = Utf8BomlessEncoding,
                Indent             = true,
                OmitXmlDeclaration = true,
            }))
            {
                projectRoot.WriteTo(xw);
            }

            var csFilePath = Path.Combine(workingDirPath, "Program.cs");
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

            // TODO User-supplied dotnet.cmd

            foreach (var ext in new[] { ".cmd", ".sh" })
                GenerateBatch(LoadTextResource("dotnet" + ext), ext, queryFilePath, packagesPath, rs);
        }

        static Version GetLatestPackageVersion(string id, bool isPrereleaseAllowed)
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

            var xml = new WebClient().DownloadString(url);

            var versions =
                from e in XDocument.Parse(xml)
                                   .Element(atom + "feed")
                                   .Elements(atom + "entry")
                select new Version((string) e.Element(m + "properties")
                                             .Element( d + "Version"));

            return versions.SingleOrDefault();
        }

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
