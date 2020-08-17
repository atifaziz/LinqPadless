#region Copyright (c) 2020 Atif Aziz. All rights reserved.
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
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
    using CSharpMinifier;
    using NuGet.Versioning;

    static class Script
    {
        enum RewriterState
        {
            ScanReferenceOrUsing,
            ScanUsing,
            Using,
            UsingTrailingSpace,
            Skip,
        }

        public static (XElement, string) Transpile(LinqPadQueryLanguage language, string source)
        {
            var state = RewriterState.ScanReferenceOrUsing;
            var imports = new List<string>();
            var packages = new List<PackageReference>();
            var sb = new StringBuilder(source.Length);
            var ibs = new StringBuilder();

            using var e = Scanner.Scan(source).GetEnumerator();
            while (e.TryRead(out var token))
            {
                restart:
                switch (state)
                {
                    case RewriterState.ScanReferenceOrUsing:
                    {
                        switch (token.Kind)
                        {
                            case TokenKind.PreprocessorDirective:
                            {
                                var directive = source.Substring(token.Start.Offset, token.Length);
                                var match = Regex.Match(directive, @"^#r\s+""nuget:\s*(\w+(?:[.-]\w+)*)(?:\s*,\s*(.+))?""\s*$");
                                if (!match.Success)
                                {
                                    state = RewriterState.Skip;
                                    goto default;
                                }
                                var groups = match.Groups;
                                var id = groups[1].Value;
                                var group2 = groups[2];
                                var version = group2.Success
                                    ? NuGetVersion.TryParse(group2.Value, out var v) ? v
                                      : throw new Exception($"Invalid package version in reference (line {token.Start.Line}): {directive}")
                                    : null;
                                packages.Add(new PackageReference(id, version, version?.IsPrerelease ?? false));
                                while (e.TryRead(out var t) && t.Kind != TokenKind.NewLine) { /* NOP */ }
                                break;
                            }
                            case TokenKind.Text:
                            {
                                state = RewriterState.ScanUsing;
                                goto restart;
                            }
                            default:
                            {
                                sb.Append(source, token.Start.Offset, token.Length);
                                break;
                            }
                        }
                        break;
                    }
                    case RewriterState.ScanUsing:
                    {
                        switch (token.Kind)
                        {
                            case TokenKind.Text:
                            {
                                var text = source.AsSpan(token.Start.Offset, token.Length);
                                if (text.SequenceEqual("using"))
                                {
                                    ibs.Clear();
                                    state = RewriterState.Using;
                                    break;
                                }
                                state = RewriterState.Skip;
                                goto default;
                            }
                            case TokenKind.PreprocessorDirective:
                            {
                                var directive = source.AsSpan(token.Start.Offset, token.Length);
                                if (directive.SequenceEqual("#r") || directive.StartsWith("#r "))
                                    throw new Exception($"The reference on line {token.Start.Line} must precede the first import: {directive.ToString()}");
                                goto default;
                            }
                            default:
                            {
                                sb.Append(source, token.Start.Offset, token.Length);
                                break;
                            }
                        }
                        break;
                    }
                    case RewriterState.Using:
                    {
                        switch (token.Kind)
                        {
                            case TokenKind.WhiteSpace:
                            {
                                if (ibs.Length > 0 && ibs[^1] != ' ')
                                    ibs.Append(' ');
                                break;
                            }
                            case TokenKind.MultiLineComment:
                            {
                                sb.Append(source, token.Start.Offset, token.Length);
                                break;
                            }
                            case TokenKind.SingleLineComment:
                            case TokenKind.NewLine:
                            {
                                while (char.IsWhiteSpace(ibs[^1]))
                                    ibs.Length -= 1;
                                imports.Add(ibs.ToString());
                                state = RewriterState.UsingTrailingSpace;
                                break;
                            }
                            case TokenKind.Text:
                            {
                                var text = source.AsSpan(token.Start.Offset, token.Length);

                                var i = text.IndexOf(';');
                                if (i < 0)
                                {
                                    ibs.Append(text);
                                }
                                else
                                {
                                    ibs.Append(text.Slice(0, i));
                                    text = text.Slice(i + 1);
                                    if (text.Length == 0)
                                    {
                                        imports.Add(ibs.ToString());
                                        state = RewriterState.UsingTrailingSpace;
                                    }
                                    else if (text.SequenceEqual("using"))
                                    {
                                        ibs.Clear();
                                    }
                                    else
                                    {
                                        imports.Add(ibs.ToString());
                                        sb.Append(text);
                                        state = RewriterState.Skip;
                                    }
                                }
                                break;
                            }
                            default:
                            {
                                throw new Exception($"Syntax error parsing import on line {token.Start.Line}, column {token.Start.Column}.");
                            }
                        }
                        break;
                    }
                    case RewriterState.UsingTrailingSpace:
                    {
                        if (token.Kind.HasTraits(TokenKindTraits.WhiteSpace))
                            break;
                        state = RewriterState.ScanUsing;
                        goto restart;
                    }
                    case RewriterState.Skip:
                    {
                        sb.Append(source, token.Start.Offset, token.Length);
                        break;
                    }
                    default:
                        throw new Exception("Internal implementation error.");
                }
            }

            return (new XElement("Query", new XAttribute("Kind", language),
                    from pr in packages
                    select new XElement("NuGetReference",
                                        pr.HasVersion ? new XAttribute("Version", pr.Version) : null,
                                        pr.Id),
                    from ns in imports
                    select new XElement("Namespace", ns)),
                    sb.ToString());
        }
    }
}
