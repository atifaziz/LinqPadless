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
        int _disposed;

        public void Dispose()
        {
            var disposed = _disposed;
            if (disposed != 0 || Interlocked.CompareExchange(ref _disposed, 1, disposed) != disposed)
                return;
            OnDispose();
        }

        /// <remarks>
        /// This method is guaranteed to be called only once.
        /// </remarks>

        protected abstract void OnDispose();
    }

    class Temp<T> : Disposable
    {
        T _resource;
        Action<T> _onDispose;
        Action<T, Exception> _onError;

        public Temp(T resource, Action<T> onDispose,
                                Action<T, Exception> onError)
        {
            _resource = resource;
            _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
            _onError = onError;
        }

        protected T Resource => _resource;

        protected override void OnDispose()
        {
            var resource  = Reset(ref _resource);
            var onDispose = Reset(ref _onDispose);
            var onError   = Reset(ref _onError);

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

    sealed class TempFile : Temp<string>
    {
        public TempFile(string path) : this(path, null) {}

        public TempFile(string path, Action<string, Exception> onError) :
            base(path, File.Delete, onError) {}

        public string Path => Resource;

        public override string ToString() => Path;
    }
}
