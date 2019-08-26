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
    using static Optuple.OptionModule;

    static class TryModule
    {
        public static (bool, TResult)
            Try<T, TResource, TException, TResult>(
                T arg, Func<T, TResource> resourceFactory,
                Func<TResource, TResult> function,
                Func<TException, bool> errorPredicate)
            where TResource : IDisposable
            where TException : Exception
        {
            if (resourceFactory == null) throw new ArgumentNullException(nameof(resourceFactory));
            if (function == null) throw new ArgumentNullException(nameof(function));
            if (errorPredicate == null) throw new ArgumentNullException(nameof(errorPredicate));

            var resource = resourceFactory(arg);
            try
            {
                return Some(function(resource));
            }
            catch (TException ex) when (errorPredicate(ex))
            {
                return default;
            }
            finally
            {
                resource.Dispose();
            }
        }
    }
}
