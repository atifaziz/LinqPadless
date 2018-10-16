#pragma warning disable 105
// CS0105: The using directive for 'namespace' appeared previously in this namespace
// https://docs.microsoft.com/en-us/dotnet/csharp/misc/cs0105

using System;
using System.CodeDom.Compiler;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Reactive.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using WebLinq;
using WebLinq.Modules;

// {% imports %}

// {% generator %}

static class Program
{
    static readonly IHttpClient Http = HttpClient.Default.Wrap(async (send, req, config) =>
    {
        Logger.Log($"> HTTP {req.Method} {req.RequestUri}", ConsoleColor.DarkCyan);
        var rsp = await send(req, config);
        var statusCode = (int) rsp.StatusCode;
        var responseColor = statusCode >= 200 && statusCode < 400
                          ? ConsoleColor.DarkGreen
                          : ConsoleColor.DarkRed;
        Logger.Log($"< HTTP {(int) rsp.StatusCode} ({rsp.StatusCode}) {rsp.ReasonPhrase}"
                   + (rsp.Content.Headers.ContentType is MediaTypeHeaderValue h ? " (" + h + ")" : null),
                   responseColor);
        return rsp;
    });

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

    static async System.Threading.Tasks.Task __run__<T>(IObservable<T> source, [System.Runtime.CompilerServices.CallerFilePath] string path = null, string code = null)
    {
        if (code == null)
            code = File.ReadAllText(path);

        var startTime = DateTimeOffset.Now;
        Logger.Log($"{Path.GetFileNameWithoutExtension(path)} started at {startTime}.");
        if (Assembly.GetExecutingAssembly().GetCustomAttribute<GeneratedCodeAttribute>() is GeneratedCodeAttribute gc)
            Logger.Log($"Generator: {gc.Tool} ({gc.Version})");

        var map =
            Regex.Matches(code, @"
                          [^/]/{4}
                          [\x20\t]* ([^/].*?)
                          [\x20\t]* =
                          [\x20\t]* ([\w_][\w0-9_]+)",
                          RegexOptions.IgnorePatternWhitespace)
                 .ToLookup(m => m.Groups[2].Value, m => m.Groups[1].Value)
                 .ToDictionary(e => e.Key, e => e.Last(), StringComparer.OrdinalIgnoreCase);

        if (typeof(T).IsPrimitive || Type.GetTypeCode(typeof(T)) != TypeCode.Object)
        {
            Console.WriteLine(Enquote(typeof(T).Name));
            await source.Select(Enquote).Do(Console.WriteLine);
        }
        else
        {
            var properties =
                typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(p => p.CanRead && !p.CanWrite && p.GetIndexParameters().Length == 0)
                         .Select(p => new
                         {
                             Name = map.TryGetValue(p.Name, out var name) ? name : SnakeCaseScreamingFromPascal(p.Name),
                             GetValue = new Func<object, object>(p.GetValue),
                         })
                         .ToArray();

            if (!properties.Any())
                throw new Exception("");

            Console.WriteLine(string.Join(",", from p in properties select Enquote(p.Name)));

            await source.Do(e =>
                Console.WriteLine(
                    string.Join(",",
                        from p in properties
                        select Enquote(p.GetValue(e)))));

            var endTime = DateTimeOffset.Now;
            Logger.Log($"Time taken was {endTime - startTime}.");
        }

        string Enquote<TValue>(TValue value) =>
            "\"" + Convert.ToString(value, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\"";

        string SnakeCaseScreamingFromPascal(string s) =>
            Regex.Replace(s, @"((?<![A-Z]|^)[A-Z]|(?<=[A-Z]+)[A-Z](?=[a-z]))", m => "_" + m.Value)
                 .ToUpperInvariant();
    }
}

static class Util
{
    public static T Dump<T>(this T value,
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
    {
        Logger.Log($"{Path.GetFileName(sourceFilePath)}@{sourceLineNumber}:{value}", ConsoleColor.DarkBlue);
        return value;
    }
}

static class Logger
{
    public static void Log(string line, ConsoleColor backgroundColor = ConsoleColor.DarkGray)
    {
        ConsoleColor? oldBackgroundColor = default;
        ConsoleColor? oldForegroundColor = default;

        if (!Console.IsErrorRedirected)
        {
            oldBackgroundColor = Console.BackgroundColor;
            Console.BackgroundColor = backgroundColor;

            oldForegroundColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;
        }

        Console.Error.Write(line);
        Console.Error.Flush();

        if (oldBackgroundColor is ConsoleColor bc)
            Console.BackgroundColor = bc;

        if (oldForegroundColor is ConsoleColor fc)
            Console.ForegroundColor = fc;

        Console.Error.WriteLine();
    }
}
