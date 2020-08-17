namespace Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using LinqPadless;
    using Markdig;
    using Markdig.Syntax;
    using Markdig.Syntax.Inlines;
    using Xunit;
    using static MoreLinq.Extensions.ZipLongestExtension;
    using static MoreLinq.Extensions.IndexExtension;

    public class ScriptTests
    {
        public enum QueryKind { Expression, Statements, Program }

        //[Fact]
        public void Test2()
        {
            foreach (var args in TestSource())
                Test((TestRecord)args[0]);
        }

        [Theory]
        [MemberData(nameof(TestSource))]
        public void Test(TestRecord record)
        {
            var kind = record.QueryKind;
            var source = record.Source;
            var expected = record.Expected;

            var language = kind switch
            {
                QueryKind.Expression => LinqPadQueryLanguage.Expression,
                QueryKind.Statements => LinqPadQueryLanguage.Statements,
                QueryKind.Program => LinqPadQueryLanguage.Program
            };

            string actual;

            if (record.IsError)
            {
                var e = Assert.Throws<Exception>(() => Script.Transpile(language, source));
                actual = e.Message;
            }
            else
            {
                var (meta, code) = Script.Transpile(language, source);
                actual = string.Join(Environment.NewLine, meta, string.Empty, code);
            }

            foreach (var (i, (exp, act)) in
                Regex.Split(expected, @"\r?\n")
                     .ZipLongest(Regex.Split(actual, @"\r?\n"),
                                 ValueTuple.Create)
                     .Index(1))
            {
                Assert.Equal((i, exp), (i, act));
            }

            Assert.Equal(Regex.Split(expected, @"\r?\n"), Regex.Split(actual, @"\r?\n"));

        }

        public sealed class TestRecord
        {
            public string    Title     { get; }
            public QueryKind QueryKind { get; }
            public string    Source    { get; }
            public bool      IsError   { get; }
            public string    Expected  { get; }

            public TestRecord(string title, QueryKind queryKind, string source, bool isError, string expected)
            {
                Title = title;
                QueryKind = queryKind;
                Source = source;
                IsError = isError;
                Expected = expected;
            }

            public override string ToString() => Title;
        }

        public static TheoryData<TestRecord> TestSource()
        {
            using var stream = typeof(ScriptTests).Assembly.GetManifestResourceStream(typeof(ScriptTests), nameof(ScriptTests) + ".md");
            using var reader = new StreamReader(stream);
            var md = reader.ReadToEnd();
            var document = Markdown.Parse(md);

            var data = new TheoryData<TestRecord>();
            var headings = new Stack<HeadingBlock>();

            using var be = document.AsEnumerable().GetEnumerator();
            while (be.TryRead(out var block))
            {
                restart:

                if (!(block is HeadingBlock heading))
                    continue;

                while (headings.Count > 0 && heading.Level <= headings.Peek().Level)
                    headings.Pop();
                headings.Push(heading);
                var title = string.Join(" / ", from h in headings.Reverse().Skip(1)
                                               select ((LiteralInline)h.Inline.Single()).Content.ToString());
                var kind = QueryKind.Expression;
                var source = default(string);
                var isError = false;
                var expected = default(string);

                while (true)
                {
                    var more = be.TryRead(out block);
                    if (!more || block is HeadingBlock)
                    {
                        if (source != null || expected != null)
                            data.Add(new TestRecord(title, kind, source, isError, expected));
                        if (more)
                            goto restart;
                        break;
                    }

                    if (block is ParagraphBlock paragraph
                        && paragraph.Inline.SingleOrDefault() is LiteralInline literal)
                    {
                        var i = block.Parent.IndexOf(block);
                        var nextBlock = i + 1 < block.Parent.Count ? block.Parent[i + 1] : null;

                        switch (literal.Content.ToString().Replace(" ", null).ToLowerInvariant())
                        {
                            case "suppose:" when nextBlock is ListBlock list:
                            {
                                var kinds =
                                    from ListItemBlock item in list
                                    select item.SingleOrDefault() is ParagraphBlock paragraph
                                           && paragraph.Inline.SingleOrDefault() is LiteralInline literal
                                        ? Regex.Match(literal.Content.ToString(), @"(\w+)\s+is\s+(\w+)")
                                        : null
                                    into m
                                    where m?.Success ?? false
                                    select KeyValuePair.Create(m.Groups[1].Value, m.Groups[2].Value) into e
                                    where "kind".Equals(e.Key, StringComparison.OrdinalIgnoreCase)
                                    select e.Value;
                                kind = Enum.Parse<QueryKind>(kinds.Last(), true);
                                break;
                            }
                            case "source:" when nextBlock is FencedCodeBlock code:
                            {
                                source = code.Lines.ToSlice().ToString();
                                break;
                            }
                            case "expected:" when nextBlock is FencedCodeBlock code:
                            {
                                expected = code.Lines.ToSlice().ToString();
                                break;
                            }
                            case "errorexpected:" when nextBlock is FencedCodeBlock code:
                            {
                                isError = true;
                                expected = code.Lines.ToSlice().ToString();
                                break;
                            }
                        }
                    }
                }
            }

            return data;
        }
    }
}
