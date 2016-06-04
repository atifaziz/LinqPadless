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

            var options = new OptionSet
            {
                { "?|help|h" , "prints out the options", _ => help = true },
                { "verbose|v", "enable additional output", _ => verbose = true },
                { "d|debug"  , "debug break", _ => Debugger.Launch() },
                { "r|recurse", "include sub-directories", _ => recurse = true },
                { "f|force"  , "force continue on errors", _ => force = true },
                { "w|watch"  , "watch for changes and re-compile", _ => watching = true },
            };

            var tail = options.Parse(args);

            if (verbose)
                Trace.Listeners.Add(new ConsoleTraceListener(useErrorStream: true));

            if (help || tail.Count == 0)
            {
                Help(options);
                return;
            }

            var repo = PackageRepositoryFactory.Default.CreateRepository("https://packages.nuget.org/api/v2");
            var queries = GetQueries(tail, recurse);

            const string packagesDirName = "packages";

            if (watching)
            {
                if (tail.Count > 1)
                {
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

                        var outdatedQueries =
                            // ReSharper disable once PossibleMultipleEnumeration
                            from q in queries
                            let csx = new FileInfo(Path.ChangeExtension(q, ".csx"))
                            where !csx.Exists
                                  || File.GetLastWriteTime(q) > csx.LastWriteTime
                            select q;

                        var count = 0;
                        var compiledCount = 0;
                        // ReSharper disable once LoopCanBeConvertedToQuery
                        // ReSharper disable once LoopCanBePartlyConvertedToQuery
                        foreach (var query in outdatedQueries)
                        {
                            var compiled = Compile(query, repo, packagesDirName, force, verbose);
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
                    Compile(query, repo, packagesDirName, force, verbose);
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

        static IEnumerable<string> GetQueries(IEnumerable<string> tail, bool includeSubdirs)
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

        static bool Compile(string queryFilePath,
                            IPackageRepository repo,
                            string packagesDirName,
                            bool force = false,
                            bool verbose = false)
        {
            var w0 = IndentingLineWriter.Create(Console.Out);
            w0.WriteLine($"Compiling {queryFilePath}");
            var w1 = w0.Indent();

            var eomLineNumber = LinqPad.GetEndOfMetaLineNumber(queryFilePath);
            var lines = File.ReadLines(queryFilePath);

            var xml = string.Join(Environment.NewLine,
                          // ReSharper disable once PossibleMultipleEnumeration
                          lines.Take(eomLineNumber));

            var query = XElement.Parse(xml);

            if (verbose)
                w1.Write(query);

            if (!"Statements".Equals((string) query.Attribute("Kind"), StringComparison.OrdinalIgnoreCase))
            {
                var error = new NotSupportedException("Only Statements LINQPad queries are supported in this version.");
                if (force)
                {
                    w1.WriteLine($"WARNING! {error.Message}");
                    return false;
                }
                throw error;
            }

            var nrs =
                from nr in query.Elements("NuGetReference")
                select new
                {
                    Id           = (string)nr,
                    Version      = SemanticVersion.ParseOptionalVersion((string) nr.Attribute("Version")),
                    IsPrerelease = (bool?)nr.Attribute("Prerelease") ?? false
                };

            nrs = nrs.ToArray();

            if (verbose && nrs.Any())
            {
                w1.WriteLine($"Packages referenced ({nrs.Count():N0}):");
                w1.Indent().WriteLines(
                    from nr in nrs
                    select nr.Id + (nr.Version != null ? " " + nr.Version : null)
                                 + (nr.IsPrerelease ? " (pre-release)" : null));
            }

            var queryDirPath = Path.GetFullPath(// ReSharper disable once AssignNullToNotNullAttribute
                                                Path.GetDirectoryName(queryFilePath));

            var packagesPath = Path.Combine(queryDirPath, packagesDirName);
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
                var pkg = pm.LocalRepository.FindPackage(nr.Id, nr.Version);
                if (pkg == null)
                {
                    pkg = repo.FindPackage(nr.Id, nr.Version,
                                           allowPrereleaseVersions: nr.IsPrerelease,
                                           allowUnlisted: false);
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

            w1.WriteLine($"Resolved references ({references.Count:N0}):");
            w1.Indent().WriteLines(from r in references select r.AssemblyPath);

            var outputs =
                from ls in new[]
                {
                    from rs in new[]
                    {
                        LinqPad.DefaultReferences,

                        from r in query.Elements("Reference")
                        select (string)r into r
                        select r.StartsWith(LinqPad.RuntimeDirToken, StringComparison.OrdinalIgnoreCase)
                             ? r.Substring(LinqPad.RuntimeDirToken.Length)
                             : r,


                        from r in references select r.AssemblyPath,
                    }
                    from r in rs
                    select $"#r \"{r}\"",

                    from nss in new[]
                    {
                        LinqPad.DefaultNamespaces,

                        from ns in query.Elements("Namespace")
                        select (string)ns,
                    }
                    from ns in nss
                    select $"using {ns};",

                    // ReSharper disable once PossibleMultipleEnumeration
                    lines.Skip(eomLineNumber - 1),
                }
                from line in ls.Concat(new[] { string.Empty })
                select line;

            File.WriteAllLines(Path.ChangeExtension(queryFilePath, ".csx"), outputs);

            var cmd = LoadTextResource("csi.cmd");

            var installs =
                from pkgdir in new[]
                {
                    MakeRelativePath(queryDirPath + Path.DirectorySeparatorChar,
                                     packagesPath + Path.DirectorySeparatorChar)
                }
                from r in references
                select $"if not exist \"{r.AssemblyPath}\" nuget install{(!r.Package.IsReleaseVersion() ? " -Prerelease" : null)} {r.Package.Id} -Version {r.Package.Version} -OutputDirectory {pkgdir.TrimEnd(Path.DirectorySeparatorChar)} || goto :pkgerr";

            cmd = Regex.Replace(cmd, @"^ *(::|rem) *__PACKAGES__",
                                string.Join(Environment.NewLine, installs),
                                RegexOptions.CultureInvariant
                                | RegexOptions.IgnoreCase
                                | RegexOptions.Multiline);

            cmd = cmd.Replace("__LINQPADLESS__", VersionInfo.FileVersion);

            File.WriteAllText(Path.ChangeExtension(queryFilePath, ".cmd"), cmd);

            return true;
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
    }
}
