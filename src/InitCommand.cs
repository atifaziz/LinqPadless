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
    using System.IO.Compression;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reactive.Disposables;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;
    using NuGet.Versioning;
    using Optuple;
    using Optuple.Collections;
    using Optuple.Linq;
    using static MoreLinq.Extensions.ToDelimitedStringExtension;
    using static MoreLinq.Extensions.MaxByExtension;
    using static Optuple.OptionModule;
    using static TryModule;
    using static OptionTag;

    #endregion

    partial class Program
    {
        static async Task<int> InitCommand(IEnumerable<string> args)
        {
            var help = Ref.Create(false);
            var verbose = Ref.Create(false);
            var force = false;
            var outputDirectoryPath = (string)null;
            var example = false;
            var specificVersion = (NuGetVersion)null;
            var feedDirPath = (string)null;
            var searchPrereleases = false;
            var isGlobalSetup = false;

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
                { "g|global", "set-up globally/user-wide", _ => isGlobalSetup = true },
            };

            var tail = options.Parse(args);

            if (tail.Count > 1)
                throw new Exception("Invalid argument: " + tail[1]);

            var source = tail.FirstOrNone().Or("LinqPadless.Templates.Template");

            var log = verbose ? Console.Error : null;
            if (log != null)
                Trace.Listeners.Add(new TextWriterTraceListener(log));

            if (help)
            {
                Help(options);
                return 0;
            }

            if (isGlobalSetup)
            {
                if (!(outputDirectoryPath is null))
                    throw new Exception(@"The ""global"" and ""output"" options are mutually exclusive.");
                outputDirectoryPath = GlobalPath;
            }
            else if (outputDirectoryPath is null)
            {
                outputDirectoryPath = Environment.CurrentDirectory;
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
                                .MaxBy(p => p.Version)
                                .FirstOrDefault();
                    }
                    else
                    {
                        version = GetLatestPackageVersion(id, searchPrereleases, url =>
                        {
                            log?.WriteLine($"Searching latest version of {id}: {url.OriginalString}");
                            return http.Value.GetStringAsync(url).GetAwaiter().GetResult();
                        });
                    }

                    if (version is null)
                        throw new Exception($"Package {id} does not exist or has not been released.");

                    log?.WriteLine($"{id} -> {version}");
                }
                else if (feedDirPath != null)
                {
                    localPackagePath =
                        ListPackagesFromFileSystemFeed(feedDirPath)
                            .FirstOrNone(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)
                                           && p.Version == version) switch
                            {
                                (SomeT, var (_, _, lpp)) => lpp,
                                _ => throw new Exception($"Package {id} does not exist or does not have version {version}."),
                            };
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
                File.WriteAllLines(Path.Join(outputDirectoryPath, "Example.linq"), encoding: Utf8.BomlessEncoding, contents: new []
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

            static HttpClient CreateHttpClient() =>
                new HttpClient(new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.Deflate
                                           | DecompressionMethods.GZip
                });
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

                var (_, file) =
                    zip.Entries.SingleOrNone(e => e.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)
                                               && e.FullName == e.Name);

                if (file == null)
                    continue;

                var (haveResult, result) =
                    from nuspec in Try(file, f => f.Open(), XDocument.Load, (XmlException e) => true)
                    from p      in nuspec.Elements().SingleOrNone(e => e.Name.LocalName == "package")
                    from md     in p     .Elements().SingleOrNone(e => e.Name.LocalName == "metadata")
                    from id     in md    .Elements().SingleOrNone(e => e.Name.LocalName == "id")
                    from vs     in md    .Elements().SingleOrNone(e => e.Name.LocalName == "version")
                    from v      in NuGetVersion.TryParse((string)vs, out var v) ? Some(v) : default
                    select (((string)id).Trim(), v, nupkg);

                if (haveResult)
                    yield return result;
            }
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
    }
}
