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
    using System.Runtime.Versioning;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Xml.Linq;
    using Mannex;
    using Mannex.IO;
    using NDesk.Options;
    using NuGet;

    #endregion

    static partial class Program
    {
        static partial void Wain(IEnumerable<string> args)
        {
            var verbose = false;
            var help = false;
            var recurse = false;
            var force = false;
            var watching = false;
            var incremental = false;
            var extraPackageList = new List<PackageReference>();
            var extraImportList = new List<string>();

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
            };

            var tail = options.Parse(args.TakeWhile(arg => arg != "--"));

            if (verbose)
                Trace.Listeners.Add(new ConsoleTraceListener(useErrorStream: true));

            if (help || tail.Count == 0)
            {
                Help(options);
                return;
            }

            extraImportList.RemoveAll(string.IsNullOrEmpty);

            // TODO Allow package source to be specified via args
            // TODO Use default NuGet sources configuration

            var repo = PackageRepositoryFactory.Default.CreateRepository("https://packages.nuget.org/api/v2");
            var queries = GetQueries(tail, recurse);

            // TODO Allow packages directory to be specified via args

            const string packagesDirName = "packages";

            var compiler = Compiler(repo, packagesDirName, extraPackageList, extraImportList,
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

                var tokens = SplitDirFileSpec(tail.First(), (dp, fs) => new
                {
                    DirPath  = dp ?? Environment.CurrentDirectory,
                    FileSpec = fs
                });

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
                            tokens.DirPath, tokens.FileSpec,
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

        static T SplitDirFileSpec<T>(string spec, Func<string, string, T> selector)
        {
            var i = spec.LastIndexOfAny(PathSeparators);
            // TODO handle rooted cases
            return i >= 0
                 ? selector(spec.Substring(0, i + 1), spec.Substring(i + 1))
                 : selector(null, spec);
        }

        static IEnumerable<string> GetQueries(IEnumerable<string> tail,
                                              bool includeSubdirs)
        {
            var dirSearchOption = includeSubdirs
                                ? SearchOption.AllDirectories
                                : SearchOption.TopDirectoryOnly;
            return
                from spec in tail
                let tokens = SplitDirFileSpec(spec, (dp, fs) => new
                {
                    DirPath  = dp ?? Environment.CurrentDirectory,
                    FileSpec = fs,
                })
                from e in
                    tokens.FileSpec.IndexOfAny(Wildchars) >= 0
                    ? from fi in new DirectoryInfo(tokens.DirPath).EnumerateFiles(tokens.FileSpec, dirSearchOption)
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

        static Func<string, bool> Compiler(IPackageRepository repo, string packagesPath,
            IEnumerable<PackageReference> extraPackages,
            IEnumerable<string> extraImports,
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

                    var packagesFullPath = Path.GetFullPath(Path.Combine(// ReSharper disable once AssignNullToNotNullAttribute
                                                                         Path.GetDirectoryName(queryFilePath),
                                                                         packagesPath));

                    var info = Compile(queryFilePath, repo, packagesFullPath, extraPackages, extraImports, verbose, writer,
                        (kind, src, imps, refs) => new
                        {
                            Kind       = kind,
                            Source     = src,
                            Imports    = imps,
                            References = refs,
                        });

                    GenerateScripts(queryFilePath, packagesFullPath,
                                    info.Kind, info.Source, info.Imports,
                                    info.References);

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

        static T Compile<T>(string queryFilePath, IPackageRepository repo, string packagesPath,
            IEnumerable<PackageReference> extraPackageReferences,
            IEnumerable<string> extraImports,
            bool verbose, IndentingLineWriter writer,
            Func<QueryLanguage, string, IEnumerable<string>, IEnumerable<Reference>, T> selector)
        {
            var w1 = writer.Indent();

            writer.WriteLine($"{queryFilePath}");

            var eomLineNumber = LinqPad.GetEndOfMetaLineNumber(queryFilePath);
            var lines = File.ReadLines(queryFilePath);

            var xml = string.Join(Environment.NewLine,
                          // ReSharper disable once PossibleMultipleEnumeration
                          lines.Take(eomLineNumber));

            var query = XElement.Parse(xml);

            if (verbose)
                w1.Write(query);

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
                    select new PackageReference((string)nr,
                                                SemanticVersion.ParseOptionalVersion((string) nr.Attribute("Version")),
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
                w1.WriteLine($"Packages referenced ({nrs.Count():N0}):");
                w1.Indent().WriteLines(from nr in nrs select nr.Title);
            }

            w1.WriteLine($"Packages directory: {packagesPath}");
            var pm = new PackageManager(repo, packagesPath);

            pm.PackageInstalling += (_, ea) =>
                w1.WriteLine($"Installing {ea.Package}...");
            pm.PackageInstalled += (_, ea) =>
                w1.Indent().WriteLine($"Installed at {ea.InstallPath}");

            var targetFrameworkName = new FrameworkName(AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName);
            w1.WriteLine($"Packages target: {targetFrameworkName}");

            var references = Enumerable.Repeat(new { Package = default(IPackage),
                                                      AssemblyPath = default(string) }, 0)
                                        .ToList();
            foreach (var nr in nrs)
            {
                var pkg = pm.LocalRepository.FindPackage(nr.Id, nr.Version,
                                                         allowPrereleaseVersions: nr.IsPrereleaseAllowed,
                                                         allowUnlisted: false);
                if (pkg == null)
                {
                    pkg = repo.FindPackage(nr.Id, nr.Version,
                                           allowPrereleaseVersions: nr.IsPrereleaseAllowed,
                                           allowUnlisted: false);

                    if (pkg == null)
                    {
                        throw new Exception("Package not found: " + nr.Title);
                    }

                    pm.InstallPackage(pkg.Id, pkg.Version);
                }

                w1.WriteLine("Resolving references...");
                references.AddRange(GetReferencesTree(pm.LocalRepository, pkg, targetFrameworkName, w1.Indent(),
                                     (r, p) => new
                                     {
                                         Package      = p,
                                         AssemblyPath = Path.Combine(pm.PathResolver.GetInstallPath(p), r.Path)
                                     }));
            }

            var packagesPathWithTrailer = packagesPath + Path.DirectorySeparatorChar;

            references =
                references.GroupBy(e => e.Package, (k, g) => g.First())
                           .Select(r => new
                           {
                               r.Package,
                               AssemblyPath = MakeRelativePath(queryFilePath, packagesPathWithTrailer)
                                            + MakeRelativePath(packagesPathWithTrailer, r.AssemblyPath),
                           })
                           .ToList();

            if (references.Any())
            {
                w1.WriteLine($"Resolved references ({references.Count:N0}):");
                w1.Indent().WriteLines(from r in references select r.AssemblyPath);
            }

            return
                selector(
                    queryKind,
                    // ReSharper disable once PossibleMultipleEnumeration
                    string.Join(Environment.NewLine, lines.Skip(eomLineNumber - 1)),
                    LinqPad.DefaultNamespaces
                            .Concat(from ns in query.Elements("Namespace")
                                    select (string)ns)
                            .Concat(extraImports),
                    LinqPad.DefaultReferences.Select(r => new Reference(r))
                            .Concat(from r in query.Elements("Reference")
                                    select (string)r into r
                                    select r.StartsWith(LinqPad.RuntimeDirToken, StringComparison.OrdinalIgnoreCase)
                                        ? r.Substring(LinqPad.RuntimeDirToken.Length)
                                        : r into r
                                    select new Reference(r))
                            .Concat(from r in references
                                    select new Reference(r.AssemblyPath, r.Package)));
        }

        static IEnumerable<T> GetReferencesTree<T>(IPackageRepository repo,
            IPackage package, FrameworkName targetFrameworkName, IndentingLineWriter writer,
            Func<IPackageAssemblyReference, IPackage, T> selector)
        {
            writer?.WriteLine(package.GetFullName());

            IEnumerable<IPackageAssemblyReference> refs;
            if (VersionUtility.TryGetCompatibleItems(targetFrameworkName, package.AssemblyReferences, out refs))
            {
                foreach (var r in refs)
                    yield return selector(r, package);
            }

            var subrefs =
                from d in package.GetCompatiblePackageDependencies(targetFrameworkName)
                select repo.FindPackage(d.Id) into dp
                where dp != null
                from r in GetReferencesTree(repo, dp, targetFrameworkName,
                                            writer?.Indent(), selector)
                select r;

            foreach (var r in subrefs)
                yield return r;
        }

        static void GenerateScripts(string queryFilePath, string packagesPath,
            QueryLanguage queryKind,
            string source, IEnumerable<string> imports,
            IEnumerable<Reference> references)
        {
            var body = queryKind == QueryLanguage.Expression
                     ? string.Join(Environment.NewLine, "System.Console.WriteLine(", source, ");")
                     : queryKind == QueryLanguage.Program
                     ? source + Environment.NewLine + "Main();"
                     : source;

            var rs = references.ToArray();

            File.WriteAllLines(Path.ChangeExtension(queryFilePath, ".csx"),
                from lines in new[]
                {
                    from r in rs
                    select $"#r \"{r.Path}\"",

                    Seq.Return(string.Empty),

                    from ns in imports
                    select $"using {ns};",

                    Seq.Return(body, string.Empty),
                }
                from line in lines
                select line);

            // TODO User-supplied csi.cmd

            var cmd = LoadTextResource("csi.cmd");

            var queryDirPath = Path.GetFullPath(// ReSharper disable once AssignNullToNotNullAttribute
                                                Path.GetDirectoryName(queryFilePath));

            var pkgdir = MakeRelativePath(queryDirPath + Path.DirectorySeparatorChar,
                                          packagesPath + Path.DirectorySeparatorChar);

            var installs =
                from r in rs
                where r.SourcePackage != null
                select $"if not exist \"{r.Path}\" nuget install{(!r.SourcePackage.IsReleaseVersion() ? " -Prerelease" : null)} {r.SourcePackage.Id} -Version {r.SourcePackage.Version} -OutputDirectory {pkgdir.TrimEnd(Path.DirectorySeparatorChar)} >&2 || goto :pkgerr";

            cmd = Regex.Replace(cmd, @"^ *(::|rem) *__PACKAGES__",
                                string.Join(Environment.NewLine, installs),
                                RegexOptions.CultureInvariant
                                | RegexOptions.IgnoreCase
                                | RegexOptions.Multiline);

            cmd = cmd.Replace("__LINQPADLESS__", VersionInfo.FileVersion);

            File.WriteAllText(Path.ChangeExtension(queryFilePath, ".cmd"), cmd);
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
            return input.Split('@', (id, version) => new PackageReference(id, SemanticVersion.ParseOptionalVersion(version), prerelease));
        }

        sealed class PackageReference : NuGet.PackageReference
        {
            public bool IsPrereleaseAllowed { get; }

            public PackageReference(string id, SemanticVersion version, bool isPrereleaseAllowed,
                IVersionSpec versionConstraint = null, FrameworkName targetFramework = null,
                bool isDevelopmentDependency = false, bool requireReinstallation = false) :
                base(id, version, versionConstraint, targetFramework, isDevelopmentDependency, requireReinstallation)
            {
                IsPrereleaseAllowed = isPrereleaseAllowed;
            }
        }

        sealed class Reference : IEquatable<Reference>
        {
            public string Path { get; }
            public IPackage SourcePackage { get; }

            public Reference(string path, IPackage sourcePackage = null)
            {
                if (path == null) throw new ArgumentNullException(nameof(path));
                Path = path;
                SourcePackage = sourcePackage;
            }

            public bool Equals(Reference other) =>
                !ReferenceEquals(null, other)
                && (ReferenceEquals(this, other)
                    || Path == other.Path
                    && SourcePackage == other.SourcePackage);

            public override bool Equals(object obj) =>
                Equals(obj as Reference);

            public override int GetHashCode() =>
                unchecked((Path.GetHashCode() * 397) ^ (SourcePackage?.GetHashCode() ?? 0));
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
}
