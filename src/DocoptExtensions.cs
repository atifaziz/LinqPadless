#region Copyright (c) 2023 Atif Aziz. All rights reserved.
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
    using System.Collections.Generic;
    using System.Diagnostics;
    using DocoptNet;

    interface ICommonOptions
    {
        bool OptVerbose { get; }
        bool OptDebug { get; }
    }

    partial class CacheArguments : ICommonOptions { }
    partial class BundleArguments : ICommonOptions { }
    partial class InitArguments : ICommonOptions { }
    partial class InspectArguments : ICommonOptions { }
    partial class ExecuteArguments : ICommonOptions { }
    partial class HelpArguments : ICommonOptions { }

    static class DocoptExtensions
    {
        public static int Run<T>(this IHelpFeaturingParser<T> parser, string name,
                                 IEnumerable<string> args, Func<T, int> handler)
            where T : ICommonOptions =>
            parser.Parse(args)
                  .Match(args =>
                         {
                             if (args.OptVerbose)
                                 Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));

                             if (args.OptDebug)
                                 Debugger.Launch();

                             return handler(args);
                         },
                         result =>
                         {
                             Program.Help(name, result.Help, Console.Out);
                             return 0;
                         },
                         result =>
                         {
                             Program.Help(name, result.Usage, Console.Error);
                             return 1;
                         });
    }
}
