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
    using System.Xml.Linq;
    using Mannex.IO;
    using NDesk.Options;
    using NuGet;

    #endregion

    static partial class Program
    {
        static void Wain(string[] args)
        {
            var verbose = false;
            var help = false;
            var dirSearchOption = SearchOption.TopDirectoryOnly;
            var force = false;

            var options = new OptionSet
            {
                { "?|help|h" , "prints out the options", _ => help = true },
                { "verbose|v", "enable additional output", _ => verbose = true },
                { "d|debug"  , "debug break", _ => Debugger.Launch() },
                { "r|recurse", "include sub-directories", _ => dirSearchOption = SearchOption.AllDirectories },
                { "f|force"  , "force continue on errors", _ => force = true },
            };

            var tail = options.Parse(args);

            if (verbose)
                Trace.Listeners.Add(new ConsoleTraceListener(useErrorStream: true));

            if (help)
            {
                Help(options);
                return;
            }

            var wildchars = new[] { '*', '?' };
            var pathSeparators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

            var queriez =
                from spec in tail
                let i = spec.LastIndexOfAny(pathSeparators)
                let tokens = i >= 0
                           ? new { DirPath = spec.Substring(0, i + 1), FileName = spec.Substring(i + 1) }
                           : new { DirPath = Environment.CurrentDirectory, FileName = spec }
                from e in
                    tokens.FileName.IndexOfAny(wildchars) >= 0
                    ? from fi in new DirectoryInfo(tokens.DirPath).EnumerateFiles(tokens.FileName, dirSearchOption)
                      select new { File = fi, Searched = true }
                    : Directory.Exists(spec)
                    ? from fi in new DirectoryInfo(spec).EnumerateFiles("*.linq", dirSearchOption)
                      select new { File = fi, Searched = true }
                    : new[] { new { File = new FileInfo(spec), Searched = false } }
                where !e.Searched
                   || (!e.File.Name.StartsWith(".", StringComparison.Ordinal)
                       && 0 == (e.File.Attributes & (FileAttributes.Hidden | FileAttributes.System)))
                select e.File.FullName;

            var queries = queriez.ToArray();

            if (!queries.Any())
                throw new Exception("Missing LINQPad file path specification.");

            var repo = PackageRepositoryFactory.Default.CreateRepository("https://packages.nuget.org/api/v2");

            foreach (var query in queries)
                Compile(query, repo, "packages", force, verbose);
        }

        static void Compile(string queryFilePath,
                            IPackageRepository repo,
                            string packagesDirName,
                            bool force = false,
                            bool verbose = false)
        {
            Console.WriteLine($"Compiling {queryFilePath}");

            var eomLineNumber = LinqPad.GetEndOfMetaLineNumber(queryFilePath);
            var lines = File.ReadLines(queryFilePath);

            var xml = string.Join(Environment.NewLine,
                          // ReSharper disable once PossibleMultipleEnumeration
                          lines.Take(eomLineNumber));

            var query = XElement.Parse(xml);

            if (verbose)
                Console.WriteLine(query);

            if (!"Statements".Equals((string) query.Attribute("Kind"), StringComparison.OrdinalIgnoreCase))
            {
                var error = new NotSupportedException("Only Statements LINQPad queries are supported in this version.");
                if (force)
                {
                    Console.Out.WriteLineIndented(1, $"WARNING! {error.Message}");
                    return;
                }
                throw error;
            }

            var nrs =
                from nr in query.Elements("NuGetReference")
                select new
                {
                    Id = (string)nr,
                    IsPrerelease = (bool?)nr.Attribute("Prerelease") ?? false
                };

            nrs = nrs.ToArray();

            var queryDirPath = Path.GetFullPath(// ReSharper disable once AssignNullToNotNullAttribute
                                                Path.GetDirectoryName(queryFilePath));

            var packagesPath = Path.Combine(queryDirPath, packagesDirName);
            Console.Out.WriteLineIndented(1, $"Packages directory: {packagesPath}");
            var pm = new PackageManager(repo, packagesPath);

            pm.PackageInstalling += (_, ea) =>
                Console.Out.WriteLineIndented(1, $"Installing {ea.Package}...");
            pm.PackageInstalled += (_, ea) =>
                Console.Out.WriteLineIndented(1, $"Installed {ea.Package} at: {ea.InstallPath}");

            var targetFrameworkName = new FrameworkName(AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName);
            Console.Out.WriteLineIndented(1, $"Packages target: {targetFrameworkName}");

            var references = Enumerable.Repeat(new { Package = default(IPackage),
                                                      AssemblyPath = default(string) }, 0)
                                        .ToList();
            foreach (var nr in nrs)
            {
                var pkg = pm.LocalRepository.FindPackage(nr.Id);
                if (pkg == null)
                {
                    pkg = repo.FindPackage(nr.Id, (SemanticVersion)null,
                                           allowPrereleaseVersions: nr.IsPrerelease,
                                           allowUnlisted: false);
                    pm.InstallPackage(pkg.Id, pkg.Version);
                }

                references.AddRange(GetReferencesTree(pm.LocalRepository, pkg, targetFrameworkName, Console.Out, 1,
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

            foreach (var r in references)
                Console.Out.WriteLineIndented(1, r.AssemblyPath);

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

            cmd = Regex.Replace(cmd, @"^ *(::|rem) *<packages>",
                                string.Join(Environment.NewLine, installs),
                                RegexOptions.CultureInvariant
                                | RegexOptions.IgnoreCase
                                | RegexOptions.Multiline);

            File.WriteAllText(Path.ChangeExtension(queryFilePath, ".cmd"), cmd);
        }

        static void Help(OptionSet options)
        {
            var verinfo = Lazy.Create(() => FileVersionInfo.GetVersionInfo(new Uri(typeof(Program).Assembly.CodeBase).LocalPath));
            var name    = Lazy.Create(() => Path.GetFileName(verinfo.Value.FileName));
            var opts    = Lazy.Create(() => options.WriteOptionDescriptionsReturningWriter(new StringWriter { NewLine = Environment.NewLine }).ToString());
            var logo    = Lazy.Create(() => new StringBuilder().AppendLine($"{verinfo.Value.ProductName} (version {verinfo.Value.FileVersion})")
                                                               .AppendLine(verinfo.Value.LegalCopyright.Replace("\u00a9", "(C)"))
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

        static IEnumerable<T> GetReferencesTree<T>(IPackageRepository repo,
            IPackage package, FrameworkName targetFrameworkName, TextWriter log, int level,
            Func<IPackageAssemblyReference, IPackage, T> selector)
        {
            log?.WriteLine(new string(' ', level * 2) + "- " + package.GetFullName());

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
                                            log, level + 1, selector)
                select r;

            foreach (var r in subrefs)
                yield return r;
        }

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
