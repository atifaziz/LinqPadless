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

    static class Observer
    {
        public static IObserver<T> Create<T>(Action<T> onNext, Action<Exception> onError, Action onCompleted) =>
            new Observer<T>(onNext, onError, onCompleted);
    }

    sealed class Observer<T> : IObserver<T>
    {
        readonly Action<T> _onNext;
        readonly Action<Exception> _onError;
        readonly Action _onCompleted;

        public Observer(Action<T> onNext, Action<Exception> onError, Action onCompleted)
        {
            _onNext      = onNext      ?? throw new ArgumentNullException(nameof(onNext));
            _onError     = onError     ?? throw new ArgumentNullException(nameof(onError));
            _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
        }

        public void OnNext(T value) => _onNext(value);
        public void OnError(Exception error) => _onError(error);
        public void OnCompleted() => _onCompleted();
    }
}