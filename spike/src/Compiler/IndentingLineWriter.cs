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

namespace WebLinqPadQueryCompiler
{
    using System.Collections.Generic;
    using System.IO;
    using Mannex.IO;

    sealed class IndentingLineWriter
    {
        readonly TextWriter _writer;
        readonly string _indent;
        readonly string _margin;

        static readonly string DefaultIndent = new string('\x20', 2);

        public static IndentingLineWriter Create(TextWriter writer) =>
            new IndentingLineWriter(null, DefaultIndent, writer);

        IndentingLineWriter(string margin, string indent, TextWriter writer)
        {
            _indent = indent;
            _margin = margin;
            _writer = writer;
        }

        public void Write(object value) =>
            WriteLines(value?.ToString());

        public void WriteLines(string value)
        {
            if (value.IndexOf('\n') < 0)
            {
                WriteLine(value);
            }
            else
            {
                using (var line = new StringReader(value).ReadLines())
                    while (line.MoveNext())
                        WriteLine(line.Current);
            }
        }

        public void WriteLines(IEnumerable<string> lines)
        {
            foreach (var line in lines)
                WriteLine(line);
        }

        public void WriteLines(IEnumerator<string> line)
        {
            using (line)
                while (line.MoveNext())
                    WriteLine(line.Current);
        }

        public void WriteLine(string value) =>
            _writer.WriteLine(_margin + value);

        public IndentingLineWriter Indent(string prefix = null) =>
            new IndentingLineWriter(_margin + _indent, _indent, _writer);
    }
}