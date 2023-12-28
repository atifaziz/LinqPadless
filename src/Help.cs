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
    using System.Text.RegularExpressions;
    using Mannex.IO;

    partial class Program
    {
        internal static void Help(string help, TextWriter output)
        {
            using var reader = new StringReader(help);
            using var e = reader.ReadLines();
            while (e.MoveNext())
            {
                var line =
                    Regex.Replace(e.Current,
                                  @"\$([A-Z][A-Z_]*)\$",
                                  m => m.Groups[1].Value switch
                                  {
                                      "NAME"    => ThisAssembly.Project.AssemblyName,
                                      "PRODUCT" => ThisAssembly.Info.Product,
                                      "VERSION" => new Version(ThisAssembly.Info.FileVersion).Trim(3).ToString(),
                                      _         => string.Empty
                                  });

                output.WriteLine(line);
            }
        }
    }
}
