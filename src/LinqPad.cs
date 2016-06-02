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
        public const string RuntimeDirToken = @"<RuntimeDirectory>\";

        public static int GetEndOfMetaLineNumber(string path)
        {
            using (var reader = XmlReader.Create(path))
            {
                if (XmlNodeType.Element != reader.MoveToContent())
                    throw InvalidLinqPadFileError(path);

                var depth = reader.Depth;
                reader.ReadStartElement("Query", String.Empty);
                while (reader.Depth > depth)
                    reader.Read();

                if ("Query" != reader.LocalName || reader.NamespaceURI.Length > 0)
                    throw InvalidLinqPadFileError(path);

                return ((IXmlLineInfo)reader).LineNumber + 1;
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