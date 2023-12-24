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
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using Mannex.IO;
    using MonoOptionSet = Mono.Options.OptionSet;

    partial class Program
    {
        static readonly Lazy<FileVersionInfo> CachedVersionInfo = Lazy.Create(() => FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location));
        static FileVersionInfo VersionInfo => CachedVersionInfo.Value;

        static void Help(string command, MonoOptionSet options) =>
            Help(command, command, options);

        static void Help(string id, string command, MonoOptionSet options)
        {
            var name = Lazy.Create(() => Path.GetFileNameWithoutExtension(VersionInfo.FileName));
            var opts = Lazy.Create(() => options.WriteOptionDescriptionsReturningWriter(new StringWriter { NewLine = Environment.NewLine }).ToString());
            var product = Lazy.Create(() => VersionInfo.ProductName);
            var version = Lazy.Create(() => new Version(VersionInfo.FileVersion));

            using var stream = GetManifestResourceStream($"help.{id ?? command}.txt");
            using var reader = new StreamReader(stream);
            using var e = reader.ReadLines();
            while (e.MoveNext())
            {
                var line =
                    Regex.Replace(e.Current,
                                  @"\$([A-Z][A-Z_]*)\$",
                                  m => m.Groups[1].Value switch
                                  {
                                      "NAME"    => name.Value,
                                      "COMMAND" => command,
                                      "PRODUCT" => product.Value,
                                      "VERSION" => version.Value.Trim(3).ToString(),
                                      "OPTIONS" => opts.Value,
                                      _         => string.Empty
                                  });

                if (line.Length > 0 && line[^1] == '\n')
                    Console.Write(line);
                else
                    Console.WriteLine(line);
            }
        }

        static string LoadTextResource(string name, Encoding encoding = null) =>
            LoadTextResource(typeof(Program), name, encoding);

        static string LoadTextResource(Type type, string name, Encoding encoding = null)
        {
            using var stream = type != null
                             ? GetManifestResourceStream(type, name)
                             : GetManifestResourceStream(null, name);
            Debug.Assert(stream != null);
            using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8);
            return reader.ReadToEnd();
        }

        static Stream GetManifestResourceStream(string name) =>
            GetManifestResourceStream(typeof(Program), name);

        static Stream GetManifestResourceStream(Type type, string name) =>
            type != null ? type.Assembly.GetManifestResourceStream(type, name)
                         : Assembly.GetCallingAssembly().GetManifestResourceStream(name);
    }
}
