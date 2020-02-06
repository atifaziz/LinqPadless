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
            Scan,
            Using,
            Skip,
        }

        public static (IEnumerable<XElement>, string) Rewrite(string source)
        {
            var state = RewriterState.Scan;
            var imports = new List<string>();
            var packages = new List<PackageReference>();
            var sb = new StringBuilder(source.Length);

            using var e = Scanner.Scan(source).GetEnumerator();
            while (e.TryRead(out var token))
            {
                switch (state)
                {
                    case RewriterState.Scan:
                    {
                        switch (token.Kind)
                        {
                            case TokenKind.PreprocessorDirective:
                            {
                                var directive = source.Substring(token.Start.Offset, token.Length);
                                var match = Regex.Match(directive, @"^#r\s+""nuget:\s*(\w+(?:\.\w+)*)(?:\s*,\s*(.+))?""\s*$");
                                if (!match.Success)
                                {
                                    state = RewriterState.Skip;
                                    goto default;
                                }
                                var id = match.Groups[1].Value;
                                if (!NuGetVersion.TryParse(match.Groups[2].Value, out var version))
                                    throw new Exception($"Invalid package version in reference (line {token.Start.Line}): {directive}");
                                packages.Add(new PackageReference(id, version, version.IsPrerelease));
                                while (e.TryRead(out var t) && t.Kind != TokenKind.NewLine) { /* NOP */ }
                                break;
                            }
                            case TokenKind.Text:
                            {
                                var text = source.AsSpan(token.Start.Offset, token.Length);
                                if (text.Equals("using", StringComparison.OrdinalIgnoreCase) ||
                                    text.Equals(";using", StringComparison.OrdinalIgnoreCase))
                                {
                                    state = RewriterState.Using;
                                }
                                else
                                {
                                    state = RewriterState.Skip;
                                    goto default;
                                }
                                break;
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
                        if (token.Kind.HasTraits(TokenKindTraits.WhiteSpace) || token.Kind.HasTraits(TokenKindTraits.Comment))
                            break;

                        if (token.Kind != TokenKind.Text)
                            throw new Exception($"Syntax error parsing import on line {token.Start.Line}, column {token.Start.Column}.");

                        var text = source.AsSpan(token.Start.Offset, token.Length);
                        if (text.EndsWith(";", StringComparison.Ordinal))
                        {
                            imports.Add(text.Slice(0, text.Length - 1).ToString());
                            state = RewriterState.Scan;
                        }
                        else if (text.EndsWith(";using", StringComparison.Ordinal))
                        {
                            imports.Add(text.Slice(0, text.IndexOf(';')).ToString());
                        }
                        else
                        {
                            var i = text.IndexOf(';');
                            if (i < 0)
                            {
                                imports.Add(text.ToString());
                                state = RewriterState.Scan;
                            }
                            else
                            {
                                imports.Add(text.Slice(0, i).ToString());
                                sb.Append(text.Slice(i + 1).ToString());
                                state = RewriterState.Scan;
                            }
                        }
                        break;
                    }
                    case RewriterState.Skip:
                        sb.Append(source, token.Start.Offset, token.Length);
                        break;
                    default:
                        throw new Exception("Internal implementation error.");
                }
            }

            return (packages.Select(e => new XElement("NuGetReference", new XAttribute("Version", e.Id), e.Id))
                            .Concat(imports.Select(e => new XElement("Namespace", e))),
                    sb.ToString());
        }
    }
}
