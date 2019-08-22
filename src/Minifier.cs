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
    using System.Linq;
    using System.Xml.Linq;
    using CSharpMinifier;
    using Jazmin;
    using static MoreLinq.Extensions.ToDelimitedStringExtension;

    static class Minifier
    {
        public static string MinifyXml(string xml)
        {
            var doc = XDocument.Parse(xml, LoadOptions.None);
            doc.DescendantNodes().Append(null).OfType<XComment>().Remove();
            return doc.ToString(SaveOptions.DisableFormatting);
        }

        public static string MinifyJavaScript(string js) =>
            JavaScriptCompressor.Compress(js);

        static readonly MinificationOptions MinificationOptions =
            MinificationOptions.Default.FilterImportantComments();

        public static string MinifyCSharp(string text) =>
            CSharpMinifier.Minifier.Minify(text, "\n", MinificationOptions)
                                   .ToDelimitedString(string.Empty);
    }
}
