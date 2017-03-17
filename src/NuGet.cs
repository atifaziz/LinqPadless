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

#region Portions Copyright (c) 2014 Dave Glick
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//    
#endregion

namespace LinqPadless.NuGet
{
    #region Imports

    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using MoreLinq;
    using global::NuGet.Common;
    using global::NuGet.Configuration;
    using global::NuGet.Frameworks;
    using global::NuGet.PackageManagement;
    using global::NuGet.Packaging;
    using global::NuGet.Packaging.Core;
    using global::NuGet.ProjectManagement;
    using global::NuGet.Protocol;
    using global::NuGet.Protocol.Core.Types;
    using global::NuGet.Resolver;
    using global::NuGet.Versioning;

    #endregion

    interface IInstalledPackage
    {
        string Id { get; }
        NuGetVersion Version { get; }
    }

    sealed class NuGetClient : ILogger
    {
        readonly ISettings _settings;
        readonly SourceRepositoryProvider _sourceRepositoryProvider;
        readonly FolderNuGetProject _project;

        NuGetClient(ISettings settings, SourceRepositoryProvider sourceRepositoryProvider, FolderNuGetProject project)
        {
            _settings = settings;
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _project = project;
        }

        public static Func<DirectoryInfo, NuGetClient> CreateDefaultFactory()
        {
            var settings = Settings.LoadDefaultSettings(root: null, configFileName: null,
                                                        machineWideSettings: new MachineWideSettings());
            var srp = new SourceRepositoryProvider(settings);
            srp.AddGlobalDefaults();
            return packagesPath => new NuGetClient(settings, srp, new FolderNuGetProject(packagesPath.FullName));
        }

        public string PackagesPath => _project.Root;

        public LogHandlerSet LogHandlers { get; set; }
        public LogHandlerSet Log => LogHandlers ?? LogHandlerSet.Null;

        sealed class InstalledPackage : IInstalledPackage
        {
            readonly Lazy<PackageIdentity> _id;
            readonly string _path;
            readonly string _archivePath;

            public InstalledPackage(string path, string archivePath)
            {
                _path = path;
                _archivePath = archivePath;
                _id = Lazy.Create(GetIdentity);

                PackageIdentity GetIdentity()
                {
                    using (var par = ReadArchive())
                        return par.GetIdentity();
                }
            }

            public string Id => _id.Value.Id;
            public NuGetVersion Version => _id.Value.Version;
            public string CombinePath(string path) => Path.Combine(_path, path);
            public PackageArchiveReader ReadArchive() => new PackageArchiveReader(_archivePath);
            public override string ToString() => Id + " " + Version;
        }

        public IEnumerable<(IInstalledPackage package, string reference)>
            GetReferencesTree(IInstalledPackage package, NuGetFramework targetFramework, IndentingLineWriter writer)
        {
            return Impl((InstalledPackage) package);

            IEnumerable<(IInstalledPackage package, string reference)> Impl(InstalledPackage package_)
            {
                writer?.WriteLine(package_.ToString());

                var fxr = new FrameworkReducer();
                using (var par = package_.ReadArchive())
                {
                    var refs = par.GetReferenceItems().ToArray();
                    if (refs.Any())
                    {
                        var match = fxr.GetNearest(targetFramework, from r in refs select r.TargetFramework);
                        if (match != null)
                        {
                            foreach (var r in refs.Single(e => e.TargetFramework.Equals(match)).Items)
                                yield return (package_, package_.CombinePath(r.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)));
                        }
                        else
                        {
                            yield return (package_, null);
                        }
                    }

                    var dependencies = par.GetPackageDependencies().ToArray();
                    if (dependencies.Any())
                    {
                        var match = fxr.GetNearest(targetFramework, from d in dependencies select d.TargetFramework);
                        if (match != null)
                        {
                            var dir = LocalRepository.GetResource<DependencyInfoResource>();
                            var subrefs =
                                from d in dependencies.Single(e => e.TargetFramework.Equals(match)).Packages
                                select dir.ResolvePackages(d.Id, targetFramework, this, CancellationToken.None).Result
                                          .Where(spdi => spdi.HasVersion && d.VersionRange.Satisfies(spdi.Version))
                                          .DefaultIfEmpty()
                                          .MaxBy(sdpi => sdpi?.Version)
                                into dp
                                where dp != null
                                from r in GetReferencesTree(FindInstalledPackage(dp.Id, dp.Version, dp.Version.IsPrerelease), targetFramework, writer?.Indent())
                                select r;

                            foreach (var r in subrefs)
                                yield return r;
                        }
                    }
                }
            }
        }

        public IInstalledPackage FindInstalledPackage(string id, NuGetVersion version, bool isPrereleaseAllowed = false)
        {
            var pid = new PackageIdentity(id, version);
            return _project.PackageExists(pid)
                 ? new InstalledPackage(_project.GetInstalledPath(pid), _project.GetInstalledPackageFilePath(pid))
                 : null;
        }

        static ResolutionContext CreateResolutionContext(bool isPrereleaseAllowed = false) =>
            new ResolutionContext(DependencyBehavior.Lowest,
                                  isPrereleaseAllowed,
                                  includeUnlisted: true,
                                  versionConstraints: VersionConstraints.None);

        public Task<NuGetVersion> GetLatestVersionAsync(string id, NuGetFramework targetFramework, bool isPrereleaseAllowed) =>
            GetLatestVersionAsync(id, targetFramework, isPrereleaseAllowed, CancellationToken.None);

        public Task<NuGetVersion> GetLatestVersionAsync(string id, NuGetFramework targetFramework, bool isPrereleaseAllowed, CancellationToken cancellationToken) =>
            NuGetPackageManager.GetLatestVersionAsync(id, targetFramework, CreateResolutionContext(isPrereleaseAllowed), _sourceRepositoryProvider.GetDefaultRepositories(), this, cancellationToken);

        SourceRepository _localRepo;
        public SourceRepository LocalRepository =>
            _localRepo ?? (_localRepo = _sourceRepositoryProvider.CreateRepository(Path.Combine(_project.Root)));

        NuGetPackageManager _pm;
        public NuGetPackageManager PackageManager =>
            _pm ?? (_pm = new NuGetPackageManager(_sourceRepositoryProvider, _settings, _project.Root) { PackagesFolderNuGetProject = _project });

        public async Task<IInstalledPackage> InstallPackageAsync(string id, NuGetVersion version, bool isPrereleaseAllowed, NuGetFramework targetFramework)
        {
            var projectContext = new NuGetProjectContext
            {
                LogAction = (level, message, args) =>
                {
                    switch (level)
                    {
                        case MessageLevel.Info   : Log.Info (string.Format(message, args)); break;
                        case MessageLevel.Warning: Log.Warn (string.Format(message, args)); break;
                        case MessageLevel.Error  : Log.Error(string.Format(message, args)); break;
                        case MessageLevel.Debug  : Log.Debug(string.Format(message, args)); break;
                    }
                }
            };
            var nupkg = new PackageIdentity(id, version);
            await PackageManager.InstallPackageAsync(PackageManager.PackagesFolderNuGetProject,
                                                     nupkg, CreateResolutionContext(), projectContext,
                                                     _sourceRepositoryProvider.GetDefaultRepositories(),
                                                     Enumerable.Empty<SourceRepository>(),
                                                     CancellationToken.None)
                                .ConfigureAwait(false);
            return FindInstalledPackage(id, version, isPrereleaseAllowed);
        }

        void ILogger.LogDebug(string data)              => Log.Debug(data);
        void ILogger.LogVerbose(string data)            => Log.Debug(data);
        void ILogger.LogInformation(string data)        => Log.Info (data);
        void ILogger.LogMinimal(string data)            => Log.Info (data);
        void ILogger.LogWarning(string data)            => Log.Warn (data);
        void ILogger.LogError(string data)              => Log.Error(data);
        void ILogger.LogInformationSummary(string data) => Log.Info (data);
        void ILogger.LogErrorSummary(string data)       => Log.Error(data);

        sealed class SourceRepositoryProvider : ISourceRepositoryProvider
        {
            static readonly string[] DefaultSources =
            {
                "https://api.nuget.org/v3/index.json"
            };

            readonly List<SourceRepository> _defaultRepositories = new List<SourceRepository>();
            readonly ConcurrentDictionary<PackageSource, SourceRepository> _repositoryCache = new ConcurrentDictionary<PackageSource, SourceRepository>();
            readonly List<Lazy<INuGetResourceProvider>> _resourceProviders;

            public SourceRepositoryProvider(ISettings settings)
            {
                // Create the package source provider (needed primarily to get default sources)
                PackageSourceProvider = new PackageSourceProvider(settings);

                // Create the set of default v2 and v3 resource providers
                _resourceProviders = new List<Lazy<INuGetResourceProvider>>();
                _resourceProviders.AddRange(global::NuGet.Protocol.Core.v2.FactoryExtensionsV2.GetCoreV2(Repository.Provider));
                _resourceProviders.AddRange(Repository.Provider.GetCoreV3());

                // Add the default sources
                foreach (string defaultSource in DefaultSources)
                    AddDefaultRepository(defaultSource);
            }

            public void AddGlobalDefaults()
            {
                _defaultRepositories.AddRange(
                    from x in PackageSourceProvider.LoadPackageSources()
                    where x.IsEnabled
                    select new SourceRepository(x, _resourceProviders));
            }

            public void AddDefaultRepository(string packageSource) =>
                _defaultRepositories.Insert(0, CreateRepository(packageSource));

            public IReadOnlyList<SourceRepository> GetDefaultRepositories() =>
                _defaultRepositories;

            public SourceRepository CreateRepository(string packageSource) =>
                CreateRepository(new PackageSource(packageSource), FeedType.Undefined);

            public SourceRepository CreateRepository(PackageSource packageSource) =>
                CreateRepository(packageSource, FeedType.Undefined);

            public SourceRepository CreateRepository(PackageSource packageSource, FeedType feedType) =>
                _repositoryCache.GetOrAdd(packageSource, ps => new SourceRepository(ps, _resourceProviders));

            public IEnumerable<SourceRepository> GetRepositories() => _repositoryCache.Values;

            public IPackageSourceProvider PackageSourceProvider { get; }
        }

        sealed class MachineWideSettings : IMachineWideSettings
        {
            readonly Lazy<IEnumerable<Settings>> _settings;

            public MachineWideSettings()
            {
                _settings = Lazy.Create(LoadMachineWideSettings);

                IEnumerable<Settings> LoadMachineWideSettings()
                {
                    var baseDirectory = NuGetEnvironment.GetFolderPath(NuGetFolderPath.MachineWideConfigDirectory);
                    return global::NuGet.Configuration.Settings.LoadMachineWideSettings(baseDirectory);
                }
            }

            public IEnumerable<Settings> Settings => _settings.Value;
        }

        sealed class NuGetProjectContext : INuGetProjectContext
        {
            public Action<MessageLevel, string, object[]> LogAction { get; set; }
            public Action<string> ReportErrorAction { get; set; } = (message) => throw new Exception(message);

            public void Log(MessageLevel level, string message, params object[] args) =>
                LogAction?.Invoke(level, message, args);
            public FileConflictAction ResolveFileConflict(string message) => FileConflictAction.Ignore;
            public PackageExtractionContext PackageExtractionContext { get; set; }
            public XDocument OriginalPackagesConfig { get; set; }
            public ISourceControlManagerProvider SourceControlManagerProvider => null;
            public global::NuGet.ProjectManagement.ExecutionContext ExecutionContext => null;
            public void ReportError(string message) => ReportErrorAction?.Invoke(message);
            public NuGetActionType ActionType { get; set; }
        }
    }

    public sealed class LogHandlerSet
    {
        public static readonly LogHandlerSet Null = new LogHandlerSet();

        public Action<string> InfoHandler    { get; }
        public Action<string> WarningHandler { get; }
        public Action<string> ErrorHandler   { get; }
        public Action<string> DebugHandler   { get; }

        public LogHandlerSet(Action<string> info  = null,
                             Action<string> warn  = null,
                             Action<string> error = null,
                             Action<string> debug = null)
        {
            InfoHandler    = info;
            WarningHandler = warn;
            ErrorHandler   = error;
            DebugHandler   = debug;
        }

        public void Info (string message) => InfoHandler   ?.Invoke(message);
        public void Warn (string message) => WarningHandler?.Invoke(message);
        public void Error(string message) => ErrorHandler  ?.Invoke(message);
        public void Debug(string message) => DebugHandler  ?.Invoke(message);

        public LogHandlerSet WithInfo   (Action<string> handler) => handler != InfoHandler    ? new LogHandlerSet(handler    , WarningHandler, ErrorHandler, DebugHandler) : this;
        public LogHandlerSet WithWarning(Action<string> handler) => handler != WarningHandler ? new LogHandlerSet(InfoHandler, handler       , ErrorHandler, DebugHandler) : this;
        public LogHandlerSet WithError  (Action<string> handler) => handler != ErrorHandler   ? new LogHandlerSet(InfoHandler, WarningHandler, handler     , DebugHandler) : this;
        public LogHandlerSet WithDebug  (Action<string> handler) => handler != DebugHandler   ? new LogHandlerSet(InfoHandler, WarningHandler, ErrorHandler, handler     ) : this;
    }
}
