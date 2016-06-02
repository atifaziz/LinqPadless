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
    using NuGet;

    #endregion

    static partial class Program
    {
        static void Wain(IList<string> args, bool verbose, TextWriter log)
        {
            if (!args.Any())
                throw new Exception("Missing LINQPad file path specification.");

            var queryFilePath = args.First();
            var eomLineNumber = LinqPad.GetEndOfMetaLineNumber(queryFilePath);
            var lines = File.ReadLines(queryFilePath);

            var xml = string.Join(Environment.NewLine,
                          // ReSharper disable once PossibleMultipleEnumeration
                          lines.Take(eomLineNumber));

            var query = XElement.Parse(xml);

            if (verbose)
                log.WriteLine(query);

            if (!"Statements".Equals((string) query.Attribute("Kind"), StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Only Statements LINQPad queries are supported in this version.");

            var nrs =
                from nr in query.Elements("NuGetReference")
                select new
                {
                    Id = (string)nr,
                    IsPrerelease = (bool?)nr.Attribute("Prerelease") ?? false
                };

            nrs = nrs.ToArray();

            var repo = PackageRepositoryFactory.Default.CreateRepository("https://packages.nuget.org/api/v2");

            var queryDirPath = Path.GetFullPath(// ReSharper disable once AssignNullToNotNullAttribute
                                                Path.GetDirectoryName(queryFilePath));

            const string packagesDirName = "packages";
            var packagesPath = Path.Combine(queryDirPath, packagesDirName);
            log.WriteLine($"Packages directory = {packagesPath}");
            var pm = new PackageManager(repo, packagesPath);

            pm.PackageInstalling += (_, ea) =>
                log.WriteLine($"Installing {ea.Package}...");
            pm.PackageInstalled += (_, ea) =>
                log.WriteLine($"Installed {ea.Package} at: {ea.InstallPath}");

            // log.WriteLine(VersionUtility.DefaultTargetFramework);
            var targetFrameworkName = new FrameworkName(AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName);
            log.WriteLine($"Packages target: {targetFrameworkName}");

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

                references.AddRange(GetReferencesTree(pm.LocalRepository, pkg, targetFrameworkName, log, 0,
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

            references.ForEach(log.WriteLine);

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

            if (verbose)
            {
                outputs = outputs.ToArray();
                foreach (var line in outputs)
                    Console.WriteLine(line);
            }

            // ReSharper disable once PossibleMultipleEnumeration
            File.WriteAllLines(Path.ChangeExtension(queryFilePath, ".csx"), outputs);

            var cmd = LoadTextResource(typeof(Program), "csi.cmd");

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

        static string LoadTextResource(Type type, string name, Encoding encoding = null)
        {
            using (var stream = type.Assembly.GetManifestResourceStream(type, name))
            {
                Debug.Assert(stream != null);
                using (var reader = new StreamReader(stream, encoding ?? Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        static partial void OnProcessingArgs(IList<string> args)
        {
            var commentIndex = args.IndexOf("--");
            if (commentIndex < 0)
                return;
            for (var i = args.Count - 1; i >= commentIndex; i--)
                args.RemoveAt(i);
        }

        static bool _verbose;

        static partial void OnVerboseFlagged() => _verbose = true;
        static partial void OnDebugFlagged() => Debugger.Launch();

        static partial void Wain(IEnumerable<string> args) =>
            Wain(args.ToList(), _verbose, _verbose ? Console.Error : TextWriter.Null);


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
