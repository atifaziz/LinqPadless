using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;
using static Process;

public class TemplateTests(ITestOutputHelper output)
{
    public static readonly string TestDirectoryPath =
        Path.GetDirectoryName(typeof(TemplateTests).Assembly.Location);

    public static readonly string LinqDirectoryPath =
        Path.Join(TestDirectoryPath, "linq");

    const string BuildConfiguration =
#if DEBUG
        "Debug"
#else
        "Release"
#endif
        ;

    public static readonly Lazy<string> LplessPath = new(() =>
    {
        var isWindows =    Environment.GetEnvironmentVariable("WINDIR") != null
                        && Environment.GetEnvironmentVariable("COMSPEC") != null;

        return new DirectoryInfo(TestDirectoryPath)
            .AncestorsAndSelf()
            .Select(dir => Path.Combine(dir.FullName, "bin", BuildConfiguration, "net8.0", isWindows ? "lpless.exe" : "lpless"))
            .First(File.Exists);
    });

    void WriteLine(string s) =>
        output.WriteLine(s);

    void WriteLines(IEnumerable<string> source)
    {
        foreach (var s in source)
            WriteLine(s);
    }

    public static readonly IEnumerable<object[]> TestSource =
        from f in Directory.GetFiles(LinqDirectoryPath, "*.linq")
        select new object[] { Path.GetFileName(f) };

    [Theory]
    [MemberData(nameof(TestSource))]
    public void Test(string fileName)
    {
        var path = Path.Combine(LinqDirectoryPath, fileName);
        var content = File.ReadAllText(path);

        var expectedExitCode
            = Regex.Match(content, @"(?<=^//<\s*)[0-9]+(?=\s*$)", RegexOptions.Multiline).Value is { Length: > 0 } s
            ? int.Parse(s, NumberStyles.None, CultureInfo.InvariantCulture)
            : throw new FormatException("Missing expected exit code specification.");

        var expectedOutputLines =
            from m in Regex.Matches(content, @"(?<=^//\|).*", RegexOptions.Multiline)
            select m.Value.Trim();

        var program = LplessPath.Value;

        var (buildExitCode, result) =
            Spawn(program, ["-x", path],
                  s => "STDOUT: " + s,
                  s => "STDERR: " + s);

        WriteLines(result);

        Assert.Equal(0, buildExitCode);

        var (exitCode, output) =
            Spawn(program, path, "foo", "bar", "baz");

        WriteLines(result);

        Assert.Equal(expectedExitCode, exitCode);
        Assert.Equal(expectedOutputLines, output);
    }
}
