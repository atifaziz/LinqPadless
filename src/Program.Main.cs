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
    using System.Linq;

    #endregion

    partial class Program
    {
        static int Main(string[] args)
        {
            try
            {
                var argList = args.ToList();
                OnProcessingArgs(argList);
                if (argList.RemoveAll(MatchMaker("--debug")) > 0)
                    OnDebugFlagged();
                if (argList.RemoveAll(MatchMaker("-h", "-?", "--help")) > 0)
                    OnHelpFlagged();
                if (argList.RemoveAll(MatchMaker("-v", "--verbose")) > 0)
                    OnVerboseFlagged();
                Wain(argList);
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.GetBaseException().Message);
                Trace.TraceError(e.ToString());
                return 0xbad;
            }
        }

        static partial void Wain(IEnumerable<string> args);
        static partial void OnProcessingArgs(IList<string> args);
        static partial void OnVerboseFlagged(); // e.g. { Trace.Listeners.Add(new ConsoleTraceListener(true)); }
        static partial void OnDebugFlagged();   // e.g. { Debugger.Launch();                                   }
        static partial void OnHelpFlagged();

        [DebuggerStepThrough]
        static Predicate<T> MatchMaker<T>(params T[] searches) =>
            input => searches.Any(s => EqualityComparer<T>.Default.Equals(s, input));
    }
}
