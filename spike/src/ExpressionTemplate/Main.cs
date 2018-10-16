#pragma warning disable 105
// CS0105: The using directive for 'namespace' appeared previously in this namespace
// https://docs.microsoft.com/en-us/dotnet/csharp/misc/cs0105

using System;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using WebLinq;
using WebLinq.Modules;

// {% imports %}

// {% generator %}

static partial class Program
{
    static async System.Threading.Tasks.Task Main()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        await __run__
        (
            #line 1
            // {% source
            from sp in Http.Get(new Uri("https://news.ycombinator.com/")).Html().Content()
            let scores =
                from s in sp.QuerySelectorAll(".score")
                select new
                {
                    Id = Regex.Match(s.GetAttributeValue("id"), @"(?<=^score_)[0-9]+$").Value,
                    Score = s.InnerText,
                }.Dump()
            from e in
                from r in sp.QuerySelectorAll(".athing")
                select new
                {
                    Id = r.GetAttributeValue("id"),
                    Link = r.QuerySelector(".storylink")?.GetAttributeValue("href"),
                }
                into r
                join s in scores on r.Id equals s.Id
                select new
                {
                    r.Id,
                    Score = int.Parse(Regex.Match(s.Score, @"\b[0-9]+(?= +points)").Value),
                    r.Link,
                }
                into e
                where e.Score >= 75
                //// IDENT = Id
                //// URL   = Link
                select e
            select e
            // %}
        );
    }
}
