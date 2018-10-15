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

namespace WebLinqPadQueryCompiler
{
    #region Imports

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
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
    using MoreEnumerable = MoreLinq.MoreEnumerable;
    using static MoreLinq.Extensions.ToDelimitedStringExtension;
    using static MoreLinq.Extensions.ToDictionaryExtension;

    #endregion

    static partial class Program
    {
        static int Wain(IEnumerable<string> args)
        {
            var verbose = false;
            var help = false;
            // var recurse = false;
            var force = false;
            var extraPackageList = new List<PackageReference>();
            var extraImportList = new List<string>();
            var targetFramework = NuGetFramework.Parse(Assembly.GetEntryAssembly().GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName);

            var options = new OptionSet
            {
                { "?|help|h"      , "prints out the options", _ => help = true },
                { "verbose|v"     , "enable additional output", _ => verbose = true },
                { "d|debug"       , "debug break", _ => Debugger.Launch() },
                { "f|force"       , "force continue on errors", _ => force = true },
                { "ref|reference=", "extra NuGet reference", v => { if (!string.IsNullOrEmpty(v)) extraPackageList.Add(ParseExtraPackageReference(v)); } },
                { "imp|import="   , "extra import", v => { extraImportList.Add(v); } },
                { "fx="           , $"target framework; default: {targetFramework.GetShortFolderName()}", v => targetFramework = NuGetFramework.Parse(v) },
            };

            var tail = options.Parse(args);

            if (verbose)
                Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));

            if (help || tail.Count == 0)
            {
                Help(options);
                return 0;
            }

            var scriptPath = Path.GetFullPath(tail.First());
            var scriptArgs = tail.Skip(1);

            var query = LinqPadQuery.Load(scriptPath);
            if (!query.IsLanguageSupported)
            {
                throw new NotSupportedException("Only LINQPad " +
                                                "C# Statements and Expression queries are fully supported " +
                                                "and C# Program queries partially in this version.");
            }

            var resourceAssembly = typeof(Program).Assembly;

            var templateFiles =
                resourceAssembly
                    .GetManifestResourceNames()
                    .Where(rn => rn.IndexOf($".Templates.{query.Language}.", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(rn => (Name   : rn.Split('.')
                                              .TakeLast(2) // assume file name + "." + extension
                                              .ToDelimitedString("."),
                                   Content: Streamable.Create(() => resourceAssembly.GetManifestResourceStream(rn))))
                    .ToArray();

            var hashSource =
                templateFiles
                    .OrderBy(rn => rn.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(rn => rn.Content.Open())
                    .Concat(MoreEnumerable.From(() => File.OpenRead(query.FilePath)))
                    .ToStreamable();

            string hash;
            using (var sha = SHA1.Create())
            using (var stream = hashSource.Open())
            {
                hash = BitConverter.ToString(sha.ComputeHash(stream))
                                   .Replace("-", string.Empty)
                                   .ToLowerInvariant();
            }

            var cacheBaseDirPath = Path.Combine(Path.GetTempPath(), nameof(WebLinqPadQueryCompiler), "cache");
            var binDirPath = Path.Combine(cacheBaseDirPath, "bin", hash);

            {
                if (!force && Run() is int exitCode)
                    return exitCode;
            }

            extraImportList.RemoveAll(string.IsNullOrEmpty);

            var srcDirPath = Path.Combine(cacheBaseDirPath, "src", hash);

            Compile(query,
                    extraPackageList, extraImportList,
                    targetFramework, srcDirPath, binDirPath,
                    templateFiles,
                    verbose);

            {
                return Run() is int exitCode
                     ? exitCode
                     : throw new Exception("Internal error executing compilation.");
            }

            int? Run()
            {
                if (!Directory.Exists(binDirPath))
                    return null;

                const string runtimeConfigJsonSuffix = ".runtimeconfig.json";
                const string depsJsonSuffix = ".deps.json";

                var baseNameSearches =
                    Directory.GetFiles(binDirPath, "*.json")
                             .Select(p => p.EndsWith(runtimeConfigJsonSuffix, StringComparison.OrdinalIgnoreCase) ? p.Substring(0, p.Length - runtimeConfigJsonSuffix.Length)
                                        : p.EndsWith(depsJsonSuffix, StringComparison.OrdinalIgnoreCase) ? p.Substring(0, p.Length - depsJsonSuffix.Length)
                                        : null);
                var binPath = baseNameSearches.FirstOrDefault(p => p != null) is string s ? s + ".dll" : null;
                if (binPath == null)
                    return null;

                const string runLogFileName = "runs.log";
                var runLogPath = Path.Combine(binDirPath, runLogFileName);
                var runLogLockTimeout = TimeSpan.FromSeconds(5);
                var runLogLockName = string.Join("-", nameof(WebLinqPadQueryCompiler), hash, runLogFileName);

                void LogRun(FormattableString str) =>
                    File.AppendAllLines(runLogPath, Seq.Return(FormattableString.Invariant(str)));

                using (var runLogLock = ExternalLock.EnterLocal(runLogLockName, runLogLockTimeout))
                using (var process = Process.Start(new ProcessStartInfo
                {
                    UseShellExecute = false,
                    FileName        = "dotnet",
                    Arguments       = scriptArgs.Prepend(binPath).ToDelimitedString(" "),
                }))
                {
                    Debug.Assert(process != null);

                    var startTime = process.StartTime;
                    LogRun($"> {startTime:o} {process.Id}");
                    runLogLock.Dispose();

                    process.WaitForExit();
                    var endTime = DateTime.Now;

                    if (ExternalLock.TryEnterLocal(runLogLockName, runLogLockTimeout, out var mutex))
                    {
                        using (mutex)
                            LogRun($"< {endTime:o} {startTime:o}/{process.Id} {process.ExitCode}");
                    }

                    return process.ExitCode;
                }
            }
        }

        static void Compile(LinqPadQuery query,
            IEnumerable<PackageReference> extraPackages,
            IEnumerable<string> extraImports,
            NuGetFramework targetFramework,
            string srcDirPath, string binDirPath,
            IEnumerable<(string Name, IStreamable Content)> templateFiles,
            bool verbose = false)
        {
            var w = IndentingLineWriter.Create(Console.Error);

            var ww = w.Indent();
            if (verbose)
                ww.Write(query.MetaElement);

            var nrs =
                from nrsq in new[]
                {
                    query.PackageReferences,
                    extraPackages,
                }
                from nr in nrsq
                select new
                {
                    nr.Id,
                    nr.Version,
                    nr.IsPrereleaseAllowed,
                    Title = Seq.Return(nr.Id,
                                       nr.Version?.ToString(),
                                       nr.IsPrereleaseAllowed ? "(pre-release)" : null)
                               .Filter()
                               .ToDelimitedString(" "),
                };

            nrs = nrs.ToArray();

            if (verbose && nrs.Any())
            {
                ww.WriteLine($"Packages referenced ({nrs.Count():N0}):");
                ww.Indent().WriteLines(from nr in nrs select nr.Title);
            }

            ww.WriteLine($"Packages target: {targetFramework}");

            var isNetCoreApp = ".NETCoreApp".Equals(targetFramework.Framework, StringComparison.OrdinalIgnoreCase);

            var defaultNamespaces
                = isNetCoreApp
                ? LinqPad.DefaultCoreNamespaces
                : LinqPad.DefaultNamespaces;

            var defaultReferences
                = isNetCoreApp
                ? Array.Empty<string>()
                : LinqPad.DefaultReferences;

            var namespaces =
                defaultNamespaces
                    .Concat(query.Namespaces)
                    .Concat(extraImports);

            var references =
                defaultReferences
                    .Select(r => (r, default(PackageReference)))
                    .Concat(
                        from r in query.MetaElement.Elements("Reference")
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
                    .Concat(
                        from r in nrs
                        select ((string) null, new PackageReference(r.Id, r.Version, r.IsPrereleaseAllowed)));

            // TODO generate to a temp name and rename on success only!

            GenerateExecutable(srcDirPath, binDirPath, query, namespaces, references, templateFiles, w.Indent());
        }

        static readonly char[] PathSeparators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

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

        static IEnumerable<(string Token, string Path)> ResolvedDirTokens()
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

        static readonly Encoding Utf8BomlessEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        static void GenerateExecutable(string srcDirPath, string binDirPath,
            LinqPadQuery query, IEnumerable<string> imports,
            IEnumerable<(string Path, PackageReference SourcePackage)> references,
            IEnumerable<(string Name, IStreamable Content)> templateFiles,
            IndentingLineWriter writer)
        {
            // TODO error handling in generated code

            var workingDirPath = srcDirPath;
            if (!Directory.Exists(workingDirPath))
                Directory.CreateDirectory(workingDirPath);

            var rs = references.ToArray();

            var resourceNames =
                templateFiles
                    .ToDictionary(e => e.Name,
                                  e => e.Content,
                                  StringComparer.OrdinalIgnoreCase);

            var projectDocument =
                XDocument.Parse(resourceNames.Single(e => e.Key.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).Value.ReadText());

            var packageIdSet =
                rs.Where(e => e.SourcePackage != null)
                  .Select(e => e.SourcePackage.Id)
                  .ToHashSet(StringComparer.OrdinalIgnoreCase);

            projectDocument
                .Descendants("PackageReference")
                .Where(e => packageIdSet.Contains((string) e.Attribute("Include")))
                .Remove();

            projectDocument.Element("Project").Add(
                new XElement("ItemGroup",
                    from r in rs
                    select r.SourcePackage into package
                    select
                        new XElement("PackageReference",
                            new XAttribute("Include", package.Id),
                            package.HasVersion
                                ? new XAttribute("Version", package.Version)
                                : new XAttribute("Version", GetLatestPackageVersion(package.Id, package.IsPrereleaseAllowed)))));

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

            var csFilePath = Path.Combine(workingDirPath, "Program.cs");
            File.Delete(csFilePath);

            T Template<T>(string template, string name, Func<string, string, T> resultor)
            {
                var replacementMatch =
                    Regex.Matches(template, @"
                             (?<= ^ | \r?\n )
                             [\x20\t]* // [\x20\t]* {% [\x20\t]*([a-z]+)
                             (?: [\x20\t]* %}
                               | \s.*? // [\x20\t]* %}
                               )
                             [\x20\t]* (?=\r?\n)"
                             , RegexOptions.Singleline
                             | RegexOptions.IgnorePatternWhitespace)
                         .SingleOrDefault(m => string.Equals(m.Groups[1].Value, name, StringComparison.OrdinalIgnoreCase));

                if (replacementMatch == null)
                    throw new Exception("Internal error due to invalid template.");

                return resultor(template.Substring(0, replacementMatch.Index),
                                template.Substring(replacementMatch.Index + replacementMatch.Length));
            }

            var programTemplate = resourceNames["Program.cs"].ReadText();

            programTemplate =
                Template(programTemplate, "imports", (before, after) =>
                         before
                         + imports.GroupBy(e => e, StringComparer.Ordinal)
                                  .Select(ns => $"using {ns.First()};")
                                  .ToDelimitedString(Environment.NewLine)
                         + after);

            var source = query.Code;

            var body
                = query.Language == LinqPadQueryLanguage.Expression
                ? Template(programTemplate, "source", (before, after) =>
                      before
                      + source + Environment.NewLine
                      + ", " + SyntaxFactory.Literal(query.FilePath)
                      + ", " + SyntaxFactory.Literal(source)
                      + after).Lines()
                : query.Language == LinqPadQueryLanguage.Program
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


            if (body != null)
            {
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
            }

            // TODO User-supplied dotnet.cmd

            Spawn("dotnet", $@"publish -v q -o ""{binDirPath}"" -c Release", workingDirPath, writer,
                  exitCode => new Exception($"dotnet publish ended with a non-zero exit code of {exitCode}."));
        }

        static NuGetVersion GetLatestPackageVersion(string id, bool isPrereleaseAllowed)
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
                select NuGetVersion.Parse((string) e.Element(m + "properties")
                                                    .Element( d + "Version"));

            return versions.SingleOrDefault();
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
            var name    = Lazy.Create(() => Path.GetFileNameWithoutExtension(VersionInfo.FileName));
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
            using (var stream = type != null
                              ? GetManifestResourceStream(type, name)
                              : GetManifestResourceStream(null, name))
            {
                Debug.Assert(stream != null);
                using (var reader = new StreamReader(stream, encoding ?? Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        static Stream GetManifestResourceStream(string name) =>
            GetManifestResourceStream(typeof(Program), name);

        static Stream GetManifestResourceStream(Type type, string name) =>
            type != null ? type.Assembly.GetManifestResourceStream(type, name)
                         : Assembly.GetCallingAssembly().GetManifestResourceStream(name);

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

    enum LinqPadQueryLanguage  // ReSharper disable UnusedMember.Local
    {                          // ReSharper disable InconsistentNaming
        Unknown,
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

    sealed class LinqPadQuery
    {
        readonly Lazy<XElement> _metaElement;
        readonly Lazy<LinqPadQueryLanguage> _language;
        readonly Lazy<ReadOnlyCollection<string>> _namespaces;
        readonly Lazy<ReadOnlyCollection<PackageReference>> _packageReferences;
        readonly Lazy<string> _code;

        public string FilePath { get; }
        public string Source { get; }
        public LinqPadQueryLanguage Language => _language.Value;
        public XElement MetaElement => _metaElement.Value;
        public IReadOnlyCollection<string> Namespaces => _namespaces.Value;
        public IReadOnlyCollection<PackageReference> PackageReferences => _packageReferences.Value;
        public string Code => _code.Value;

        public static LinqPadQuery Load(string path)
        {
            var source = File.ReadAllText(path);
            var eomLineNumber = LinqPad.GetEndOfMetaLineNumber(source);
            return new LinqPadQuery(path, source, eomLineNumber);
        }

        LinqPadQuery(string filePath, string source, int eomLineNumber)
        {
            FilePath = filePath;
            Source = source;

            _metaElement = Lazy.Create(() =>
                XElement.Parse(source.Lines()
                                     .Take(eomLineNumber)
                                     .ToDelimitedString(Environment.NewLine)));

            _code = Lazy.Create(() =>
                source.Lines()
                      .Skip(eomLineNumber)
                      .ToDelimitedString(Environment.NewLine));

            _language = Lazy.Create(() =>
                Enum.TryParse((string) MetaElement.Attribute("Kind"), true, out LinqPadQueryLanguage queryKind) ? queryKind : LinqPadQueryLanguage.Unknown);

            ReadOnlyCollection<T> ReadOnlyCollection<T>(IEnumerable<T> items) =>
                new ReadOnlyCollection<T>(items.ToList());

            _namespaces = Lazy.Create(() =>
                ReadOnlyCollection(
                    from ns in MetaElement.Elements("Namespace")
                    select (string) ns));

            _packageReferences = Lazy.Create(() =>
                ReadOnlyCollection(
                    from nr in MetaElement.Elements("NuGetReference")
                    let v = (string) nr.Attribute("Version")
                    select new PackageReference((string) nr,
                               string.IsNullOrEmpty(v) ? null : NuGetVersion.Parse(v),
                               (bool?) nr.Attribute("Prerelease") ?? false)));
        }

        public bool IsLanguageSupported
            => Language == LinqPadQueryLanguage.Statements
            || Language == LinqPadQueryLanguage.Expression
            || Language == LinqPadQueryLanguage.Program;

        public override string ToString() => Source;
    }
}
