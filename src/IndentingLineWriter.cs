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
    using System.IO;
    using Mannex.IO;

    sealed class IndentingLineWriter
    {
        readonly TextWriter writer;
        readonly string indent;
        readonly string margin;

        static readonly string DefaultIndent = new('\x20', 2);

        public static IndentingLineWriter CreateUnlessNull(TextWriter writer) =>
            writer switch { null => null, var w => Create(w) };

        public static IndentingLineWriter Create(TextWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            return new IndentingLineWriter(null, DefaultIndent, writer);
        }

        IndentingLineWriter(string margin, string indent, TextWriter writer)
        {
            this.indent = indent;
            this.margin = margin;
            this.writer = writer;
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
                using var line = new StringReader(value).ReadLines();
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
            {
                while (line.MoveNext())
                    WriteLine(line.Current);
            }
        }

        public void WriteLine(string value) =>
            this.writer.WriteLine(this.margin + value);

        public IndentingLineWriter Indent() =>
            new(this.margin + this.indent, this.indent, this.writer);
    }
}
