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
    using System.Text;
    using System.Xml.Linq;
    using CSharpMinifier;
    using Mannex.IO;
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
        readonly Lazy<ReadOnlyCollection<LinqPadQueryReference>> _loads;
        readonly Lazy<string> _code;

        public string FilePath { get; }
        public string Source { get; }
        public LinqPadQueryLanguage Language => _language.Value;
        public XElement MetaElement => _metaElement.Value;
        public IReadOnlyCollection<string> Namespaces => _namespaces.Value;
        public IReadOnlyCollection<string> NamespaceRemovals => _namespaceRemovals.Value;
        public IReadOnlyCollection<PackageReference> PackageReferences => _packageReferences.Value;
        public IReadOnlyCollection<LinqPadQueryReference> Loads => _loads?.Value ?? ZeroLinqPadQueryReferences;
        public string Code => _code.Value;

        static readonly IReadOnlyCollection<LinqPadQueryReference>
            ZeroLinqPadQueryReferences = new LinqPadQueryReference[0];

        public static LinqPadQuery Load(string path) =>
            Load(path, parseLoads: true, resolveLoads: true);

        public static LinqPadQuery Parse(string source, string path) =>
            Parse(source, path, parseLoads: true, resolveLoads: true);

        public static LinqPadQuery LoadReferencedQuery(string path) =>
            Load(path, parseLoads: true, resolveLoads: false);

        public static LinqPadQuery ParseReferencedQuery(string source, string path) =>
            Parse(source, path, parseLoads: true, resolveLoads: false);

        static LinqPadQuery Load(string path, bool parseLoads, bool resolveLoads) =>
            Parse(File.ReadAllText(path), path, parseLoads, resolveLoads);

        static LinqPadQuery Parse(string source, string path, bool parseLoads, bool resolveLoads)
        {
            var eomLineNumber = LinqPad.GetEndOfMetaLineNumber(source);
            return new LinqPadQuery(path, source, eomLineNumber, parseLoads, resolveLoads);
        }

        LinqPadQuery(string filePath, string source, int eomLineNumber, bool parseLoads, bool resolveLoads)
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

            static ReadOnlyCollection<T> ReadOnlyCollection<T>(IEnumerable<T> items) =>
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

            var dirPath = Path.GetDirectoryName(FilePath);

            _loads = parseLoads switch
            {
                true =>
                   Lazy.Create(() => ReadOnlyCollection(
                       from t in Scanner.Scan(Code)
                       where t.Kind == TokenKind.PreprocessorDirective
                       select (t.Start.Line, Text: t.Substring(Code)) into t
                       select (t.Line, t.Text, Parts: t.Text.Split2(' ', StringSplitOptions.RemoveEmptyEntries)) into t
                       where t.Parts.Item1 == "#load"
                       select t.Parts.Item2 switch
                       {
                           var p when p.Length > 2 && p[0] == '"' && p[^1] == '"' => (t.Line, Path: p.Substring(1, p.Length - 2)),
                           _ => throw new Exception("Invalid load directive: " + t.Text)
                       }
                       into d
                       select (d.Line, Path: Path.DirectorySeparatorChar == '\\'
                                           ? d.Path
                                           : d.Path.Replace('\\', Path.DirectorySeparatorChar))
                       into d
                       select new LinqPadQueryReference(resolveLoads ? ResolvePath(d.Path) : null, d.Path, d.Line))),
                _ => null,
            };

            string ResolvePath(string pathSpec)
            {
                const string dots = "...";

                if (!pathSpec.StartsWith(dots, StringComparison.Ordinal))
                    return Path.GetFullPath(pathSpec, dirPath);

                var slashPath = pathSpec.AsSpan(dots.Length);

                if (slashPath.Length < 2)
                    throw InvalidLoadDirectivePathError(pathSpec);

                var slash = slashPath[0];
                if (slash != Path.DirectorySeparatorChar && slash != Path.AltDirectorySeparatorChar)
                    throw InvalidLoadDirectivePathError(pathSpec);

                var path = slashPath.Slice(1);

                foreach (var dir in new DirectoryInfo(dirPath).SelfAndParents())
                {
                    var testPath = Path.Join(dir.FullName, path);
                    if (File.Exists(testPath))
                        return testPath;
                }

                throw new FileNotFoundException("File not found: " + pathSpec);
            }

            static Exception InvalidLoadDirectivePathError(string path) =>
                throw new Exception("Invalid load directive path: " + path);
        }

        public bool IsLanguageSupported
            => Language == LinqPadQueryLanguage.Statements
            || Language == LinqPadQueryLanguage.Expression
            || Language == LinqPadQueryLanguage.Program;

        public override string ToString() => Source;
    }

    sealed class LinqPadQueryReference
    {
        readonly Lazy<LinqPadQuery> _query;
        readonly string _path;

        public LinqPadQueryReference(string path, string loadPath, int lineNumber)
        {
            _path = path;
            LoadPath = loadPath;
            LineNumber = lineNumber;
            _query = Lazy.Create(() => LinqPadQuery.LoadReferencedQuery(path));
        }

        public int LineNumber { get; }
        public string Path => _path ?? throw new InvalidOperationException();
        public string LoadPath { get; }

        public LinqPadQuery GetQuery() => _query.Value;

        public string Source => GetQuery().Source;
        public LinqPadQueryLanguage Language => GetQuery().Language;
        public XElement MetaElement => GetQuery().MetaElement;
        public IReadOnlyCollection<string> Namespaces => GetQuery().Namespaces;
        public IReadOnlyCollection<string> NamespaceRemovals => GetQuery().NamespaceRemovals;
        public IReadOnlyCollection<PackageReference> PackageReferences => GetQuery().PackageReferences;
        public string Code => GetQuery().Code;

        public override string ToString() => Source;
    }

    static partial class LinqPadQueryExtensions
    {
        public static string GetMergedCode(this LinqPadQuery query, bool skipSelf = false)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            using var lq = query.Loads.GetEnumerator();

            if (!lq.MoveNext())
                return query.Code;

            var load = lq.Current;
            var code = new StringBuilder();

            var ln = 1;
            foreach (var line in query.Code.Lines())
            {
                if (load?.LineNumber == ln)
                {
                    code.AppendLine("//>>> " + line);
                    if (load.Language switch
                        {
                            LinqPadQueryLanguage.Statements => ("RunLoadedStatements(() => {", "})"),
                            LinqPadQueryLanguage.Expression => ("DumpLoadedExpression(", ")"),
                            _ => default
                        }
                        is ({} prologue, {} epilogue))
                    {
                        code.AppendLine(prologue)
                            .Append("#line 1 \"").Append(load.Path).Append('"').AppendLine()
                            .AppendLine(load.GetQuery().FormatCodeWithLoadDirectivesCommented())
                            .Append(epilogue).Append(';').AppendLine();
                    };
                    code.AppendLine("//<<< " + line);
                    code.Append("#line ").Append(ln + 1).Append(" \"").Append(query.FilePath).Append('"').AppendLine();

                    load = lq.MoveNext() ? lq.Current : null;
                }
                else if (!skipSelf)
                {
                    code.AppendLine(line);
                }

                ln++;
            }

            return code.ToString();
        }
    }
}

namespace LinqPadless
{
    using System;
    using System.Linq;
    using static MoreLinq.Extensions.IndexExtension;
    using static MoreLinq.Extensions.ToDelimitedStringExtension;

    static partial class LinqPadQueryExtensions
    {
        public static string FormatCodeWithLoadDirectivesCommented(this LinqPadQuery query)
        {
            if (query.Loads.Count == 0)
                return query.Code;
            var lns = query.Loads.Select(e => e.LineNumber).ToHashSet();
            return query.Code.Lines()
                             .Index(1)
                             .Select(e => (lns.Contains(e.Key) ? "// " : null) + e.Value)
                             .ToDelimitedString(Environment.NewLine);
        }

        public static NotSupportedException ValidateSupported(this LinqPadQuery query) =>
            !query.IsLanguageSupported
            ? new NotSupportedException("Only LINQPad " +
                                        "C# Statements and Expression queries are fully supported " +
                                        "and C# Program queries partially in this version.")
            : null;
    }
}
