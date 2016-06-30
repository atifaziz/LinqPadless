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
    using System.Collections.Generic;
    using System.Xml;

    static class LinqPad
    {
        public static int GetEndOfMetaLineNumber(string path)
        {
            using (var reader = XmlReader.Create(path, new XmlReaderSettings
            {
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace             = true,
                IgnoreComments               = true,
            }))
            {
                // A LINQPad Query file has two parts: (1) a header in XML
                // followed by (2) the query code content.

                if (XmlNodeType.Element != reader.MoveToContent())
                    throw InvalidLinqPadFileError(path);

                if (!reader.IsStartElement("Query", string.Empty))
                    throw InvalidLinqPadFileError(path);

                try
                {
                    // Skipping will throw at the point the XML header
                    // ends and code starts because the code part will be
                    // seen as invalid XML.

                    reader.Skip();

                    // On the other hand, if Skip succeeds then it means
                    // there is not code and reader is probably sitting on
                    // EOF, so just return the line number.

                    return ((IXmlLineInfo)reader).LineNumber;
                }
                catch (XmlException e)
                {
                    return e.LineNumber - 1;
                }
            }
        }

        static Exception InvalidLinqPadFileError(string path) =>
            new Exception($"\"{path}\" does not appear to be a valid LINQPad file.");

        public static readonly ICollection<string> DefaultReferences = Array.AsReadOnly(new[]
        {
            "System.dll",
            "Microsoft.CSharp.dll",
            "System.Core.dll",
            "System.Data.dll",
            "System.Data.Entity.dll",
            "System.Transactions.dll",
            "System.Xml.dll",
            "System.Xml.Linq.dll",
            "System.Data.Linq.dll",
            "System.Drawing.dll",
            "System.Data.DataSetExtensions.dll",
        });

        public static readonly ICollection<string> DefaultNamespaces = Array.AsReadOnly(new[]
        {
            "System",
            "System.IO",
            "System.Text",
            "System.Text.RegularExpressions",
            "System.Diagnostics",
            "System.Threading",
            "System.Reflection",
            "System.Collections",
            "System.Collections.Generic",
            "System.Linq",
            "System.Linq.Expressions",
            "System.Data",
            "System.Data.SqlClient",
            "System.Data.Linq",
            "System.Data.Linq.SqlClient",
            "System.Transactions",
            "System.Xml",
            "System.Xml.Linq",
            "System.Xml.XPath",
        });
    }
}