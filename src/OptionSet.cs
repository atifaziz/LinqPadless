#region Copyright (c) 2018 Atif Aziz. All rights reserved.
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
    using Mono.Options;

    sealed class OptionSet : Mono.Options.OptionSet
    {
        readonly Func<Func<string, OptionContext, bool>, string, OptionContext, bool> _parser;

        public OptionSet(Func<Func<string, OptionContext, bool>, string, OptionContext, bool> parser) =>
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        protected override bool Parse(string argument, OptionContext c) =>
            _parser(base.Parse, argument, c);
    }
}