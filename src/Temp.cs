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
    using System;
    using System.IO;
    using System.Threading;
    using static Assignment;

    abstract class Disposable : IDisposable
    {
        int disposed;

        public void Dispose()
        {
            var disposed = this.disposed;
            if (disposed != 0 || Interlocked.CompareExchange(ref this.disposed, 1, disposed) != disposed)
                return;
            OnDispose();
        }

        /// <remarks>
        /// This method is guaranteed to be called only once.
        /// </remarks>

        protected abstract void OnDispose();
    }

    class Temp<T>(T resource, Action<T> onDispose, Action<T, Exception> onError) :
        Disposable
    {
        Action<T> onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));

        protected T Resource => resource;

        protected override void OnDispose()
        {
            Dispose(Reset(ref resource), Reset(ref this.onDispose), Reset(ref onError));

            static void Dispose(T resource, Action<T> onDispose, Action<T, Exception> onError)
            {
                try
                {
                    onDispose(resource);
                }
                catch (Exception e)
                {
                    onError?.Invoke(resource, e);
                }
            }
        }
    }

    sealed class TempFile(string path, Action<string, Exception> onError) :
        Temp<string>(path, File.Delete, onError)
    {
        public TempFile(string path) : this(path, null) { }

        public string Path => Resource;

        public override string ToString() => Path;
    }
}
