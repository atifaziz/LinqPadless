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
    #region

    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.ExceptionServices;
    using System.Threading;

    #endregion

    static class FileMonitor
    {
        public static IEnumerable<WaitForChangedResult> GetFolderChanges(
            string directoryPath,
            string fileWildcardSpecification,
            bool includeSubdirectories,
            NotifyFilters notifyFilters,
            WatcherChangeTypes changeTypes = WatcherChangeTypes.All,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var bc = new BlockingCollection<Func<WaitForChangedResult>>();

            using (var monitor = SubscribeToFolderChanges(
                directoryPath, fileWildcardSpecification,
                includeSubdirectories,
                notifyFilters, changeTypes,
                Observer.Create(
                    onNext: (WaitForChangedResult e) => bc.Add(Fun.Return(e), cancellationToken),
                    onCompleted: () => bc.CompleteAdding(),
                    onError: e =>
                    {
                        var edi = ExceptionDispatchInfo.Capture(e);
                        bc.Add(Fun.Throw<WaitForChangedResult>(edi), cancellationToken);
                    }
            )))
            {
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationToken.Register(() =>
                    {   // ReSharper disable once AccessToDisposedClosure
                        monitor.Dispose();
                        bc.CompleteAdding();
                    });
                }

                // The FileSystemWatcher raises events on a separate thread
                // so it's safe here to block on consuming the collection.

                foreach (var e in bc.GetConsumingEnumerable())
                    yield return e();
            }
        }

        public static IDisposable SubscribeToFolderChanges(
            string directoryPath,
            string fileWildcardSpecification,
            bool includeSubdirectories,
            NotifyFilters notifyFilters,
            WatcherChangeTypes changeTypes,
            IObserver<WaitForChangedResult> observer) =>
            SubscribeToFolderChanges(
                directoryPath, fileWildcardSpecification, includeSubdirectories,
                notifyFilters, observer,
                createdSelector: WaitForChangedResultor<FileSystemEventArgs>(changeTypes, WatcherChangeTypes.Created),
                changedSelector: WaitForChangedResultor<FileSystemEventArgs>(changeTypes, WatcherChangeTypes.Changed),
                deletedSelector: WaitForChangedResultor<FileSystemEventArgs>(changeTypes, WatcherChangeTypes.Deleted),
                renamedSelector: WaitForChangedResultor(changeTypes, WatcherChangeTypes.Renamed, (RenamedEventArgs e) => e.OldFullPath));

        static Func<T, WaitForChangedResult> WaitForChangedResultor<T>(
            WatcherChangeTypes changeTypes, WatcherChangeTypes changeType,
            Func<T, string> oldNameSelector = null)
            where T : FileSystemEventArgs =>
            0 == (changeTypes & changeType)
            ? null : new Func<T, WaitForChangedResult>(e => new WaitForChangedResult
            {
                ChangeType = changeType,
                Name       = e.FullPath,
                OldName    = oldNameSelector?.Invoke(e),
            });

        public static IDisposable SubscribeToFolderChanges<T>(
            string directoryPath,
            string fileWildcardSpecification,
            bool includeSubdirectories,
            NotifyFilters notifyFilters,
            IObserver<T> observer,
            Func<FileSystemEventArgs, T> createdSelector = null,
            Func<FileSystemEventArgs, T> changedSelector = null,
            Func<FileSystemEventArgs, T> deletedSelector = null,
            Func<RenamedEventArgs   , T> renamedSelector = null)
        {
            FileSystemWatcher disposable = null;
            try
            {
                var fsw = disposable = new FileSystemWatcher(directoryPath, fileWildcardSpecification)
                {
                    NotifyFilter          = notifyFilters,
                    IncludeSubdirectories = includeSubdirectories,
                };

                if (createdSelector != null) fsw.Created += (_, args) => observer.OnNext(createdSelector(args));
                if (changedSelector != null) fsw.Changed += (_, args) => observer.OnNext(changedSelector(args));
                if (deletedSelector != null) fsw.Deleted += (_, args) => observer.OnNext(deletedSelector(args));
                if (renamedSelector != null) fsw.Renamed += (_, args) => observer.OnNext(renamedSelector(args));

                fsw.Error   += (_, args) => observer.OnError(args.GetException());

                fsw.EnableRaisingEvents = true;

                disposable = null;
                return new DelegatingDisposable(() =>
                {
                    fsw.Dispose();
                    observer.OnCompleted();
                });
            }
            finally
            {
                disposable?.Dispose();
            }
        }
    }
}
