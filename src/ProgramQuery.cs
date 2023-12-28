#region Copyright (c) 2019 Atif Aziz. All rights reserved.
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
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using MoreLinq;
    using Optuple.Collections;
    using Optuple;
    using static Optuple.OptionModule;

    sealed class ProgramQuery
    {
        public MethodDeclarationSyntax Main { get; }
        public MethodDeclarationSyntax OnInit { get; }
        public MethodDeclarationSyntax OnStart { get; }
        public MethodDeclarationSyntax OnFinish { get; }
        public MethodDeclarationSyntax Hijack { get; }
        public ImmutableArray<SyntaxNode> Others { get; }
        public ImmutableArray<TypeDeclarationSyntax> Types { get; }
        public ImmutableArray<NamespaceDeclarationSyntax> Namespaces { get; }

        ProgramQuery(MethodDeclarationSyntax main,
                     MethodDeclarationSyntax onInit,
                     MethodDeclarationSyntax onStart,
                     MethodDeclarationSyntax onFinish,
                     MethodDeclarationSyntax hijack,
                     ImmutableArray<SyntaxNode> others,
                     ImmutableArray<TypeDeclarationSyntax> types,
                     ImmutableArray<NamespaceDeclarationSyntax> namespaces)
        {
            Main = main;
            OnInit = onInit;
            OnStart = onStart;
            OnFinish = onFinish;
            Hijack = hijack;
            Others = others;
            Types = types;
            Namespaces = namespaces;
        }

        enum QueryPartKind { Type, Namespace, Other }

        public static ProgramQuery Parse(string source, string path)
        {
            var syntaxTree =
                CSharpSyntaxTree.ParseText(source,
                                           options: CSharpParseOptions.Default.WithKind(SourceCodeKind.Script),
                                           path: path);

            var parts =
                syntaxTree
                    .GetRoot()
                    .ChildNodes()
                    .GroupBy(e =>
                        e switch
                        {
                            ClassDeclarationSyntax cds
                                when cds.Members.OfType<MethodDeclarationSyntax>()
                                                .Any(mds => mds.ParameterList.Parameters.Count > 0
                                                         && mds.ParameterList.Parameters.First().Modifiers.Any(m => m.IsKind(SyntaxKind.ThisKeyword))) =>
                                QueryPartKind.Type,
                            NamespaceDeclarationSyntax => QueryPartKind.Namespace,
                            _ => QueryPartKind.Other
                        })
                    .Partition(QueryPartKind.Type, QueryPartKind.Namespace, QueryPartKind.Other,
                        (tds, nsds, etc, _) => _.Any() ? throw new NotSupportedException() :
                            new
                            {
                                Types    = tds.Cast<TypeDeclarationSyntax>(),
                                Namespaces = nsds.Cast<NamespaceDeclarationSyntax>(),
                                // ReSharper disable PossibleMultipleEnumeration
                                OnInit   = FindMethod(etc, "OnInit"  , IsSimpleMethod).OrDefault(),
                                OnStart  = FindMethod(etc, "OnStart" , IsSimpleMethod).OrDefault(),
                                OnFinish = FindMethod(etc, "OnFinish", IsSimpleMethod).OrDefault(),
                                Hijack   = FindMethod(etc, "Hijack"  , IsSimpleMethod).OrDefault(),
                                Main     = FindMethod(etc, "Main")
                                               .Match(some: some => some,
                                                      none: () => throw new Exception("Program entry-point method (Main) not found.")),
                                Others   = etc,
                                // ReSharper restore PossibleMultipleEnumeration
                            });

            if (parts.Hijack != null)
                throw new NotSupportedException("The Hijack hook method is not yet supported.");

            return new ProgramQuery(parts.Main, parts.OnInit, parts.OnStart, parts.OnFinish, parts.Hijack,
                                    ImmutableArray.CreateRange(parts.Others),
                                    ImmutableArray.CreateRange(parts.Types),
                                    ImmutableArray.CreateRange(parts.Namespaces));

            static bool IsSimpleMethod(MethodDeclarationSyntax md)
                => md.ReturnType is PredefinedTypeSyntax pts
                && pts.Keyword.IsKind(SyntaxKind.VoidKeyword)
                && md.ParameterList.Parameters.Count == 0;

            static (bool, MethodDeclarationSyntax)
                FindMethod(IEnumerable<SyntaxNode> etc, string name, Func<MethodDeclarationSyntax, bool> predicate = null) =>
                    etc.Choose(e => e is MethodDeclarationSyntax md
                                    && name == md.Identifier.Text
                                    && (predicate?.Invoke(md) ?? true)
                                  ? Some(md)
                                  : default)
                       .SingleOrNone();
        }
    }
}
