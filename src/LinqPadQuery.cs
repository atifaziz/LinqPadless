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
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using MoreLinq.Extensions;
    using NuGet.Versioning;

    #endregion

    enum LinqPadQueryLanguage  // ReSharper disable UnusedMember.Global
    {                          // ReSharper disable InconsistentNaming
        Unknown,
        Expression,
        Statements,
        Program,
        VBExpression,
        VBStatements,
        VBProgram,
        FSharpExpression,
        FSharpProgram,
        SQL,
        ESQL,

        // ReSharper restore InconsistentNaming
        // ReSharper restore UnusedMember.Global
    }

    sealed class LinqPadQuery
    {
        readonly Lazy<XElement> _metaElement;
        readonly Lazy<LinqPadQueryLanguage> _language;
        readonly Lazy<ReadOnlyCollection<string>> _namespaces;
        readonly Lazy<ReadOnlyCollection<string>> _namespaceRemovals;
        readonly Lazy<ReadOnlyCollection<PackageReference>> _packageReferences;
        readonly Lazy<string> _code;

        public string FilePath { get; }
        public string Source { get; }
        public LinqPadQueryLanguage Language => _language.Value;
        public XElement MetaElement => _metaElement.Value;
        public IReadOnlyCollection<string> Namespaces => _namespaces.Value;
        public IReadOnlyCollection<string> NamespaceRemovals => _namespaceRemovals.Value;
        public IReadOnlyCollection<PackageReference> PackageReferences => _packageReferences.Value;
        public string Code => _code.Value;

        public static LinqPadQuery Load(string path) =>
            Parse(File.ReadAllText(path), path);

        public static LinqPadQuery Parse(string source, string path)
        {
            var eomLineNumber = LinqPad.GetEndOfMetaLineNumber(source);
            return new LinqPadQuery(path, source, eomLineNumber);
        }

        LinqPadQuery(string filePath, string source, int eomLineNumber)
        {
            FilePath = filePath;
            Source = source;

            _metaElement = Lazy.Create(() =>
                XElement.Parse(source.Lines()
                                     .Take(eomLineNumber)
                                     .ToDelimitedString(Environment.NewLine)));

            _code = Lazy.Create(() =>
                source.Lines()
                      .Skip(eomLineNumber)
                      .ToDelimitedString(Environment.NewLine));

            _language = Lazy.Create(() =>
                Enum.TryParse((string) MetaElement.Attribute("Kind"), true, out LinqPadQueryLanguage queryKind) ? queryKind : LinqPadQueryLanguage.Unknown);

            ReadOnlyCollection<T> ReadOnlyCollection<T>(IEnumerable<T> items) =>
                new ReadOnlyCollection<T>(items.ToList());

            _namespaces = Lazy.Create(() =>
                ReadOnlyCollection(
                    from ns in MetaElement.Elements("Namespace")
                    select (string)ns));

            _namespaceRemovals = Lazy.Create(() =>
                ReadOnlyCollection(
                    from ns in MetaElement.Elements("RemoveNamespace")
                    select (string)ns));

            _packageReferences = Lazy.Create(() =>
                ReadOnlyCollection(
                    from nr in MetaElement.Elements("NuGetReference")
                    let v = (string) nr.Attribute("Version")
                    select new PackageReference((string) nr,
                               string.IsNullOrEmpty(v) ? null : NuGetVersion.Parse(v),
                               (bool?) nr.Attribute("Prerelease") ?? false)));
        }

        public bool IsLanguageSupported
            => Language == LinqPadQueryLanguage.Statements
            || Language == LinqPadQueryLanguage.Expression
            || Language == LinqPadQueryLanguage.Program;

        public override string ToString() => Source;
    }
}
