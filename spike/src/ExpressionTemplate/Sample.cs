using System;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using WebLinq;
using WebLinq.Modules;

partial class Program
{
    static IObservable<T> Sample<T>(Func<string, int, string, T> selector) =>
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
            select e
        select selector(e.Id, e.Score, e.Link);
}
