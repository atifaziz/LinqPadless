using System;
using System.Collections.Generic;
using System.Diagnostics;

static class Process
{
    public static (int, ICollection<string>)
        Spawn(string path, params string[] args) =>
              Spawn(path, args, s => s, s => s);

    public static (int, ICollection<(T Tag, string Line)>)
        Spawn<T>(string path, T outputTag, T errorTag, params string[] args) =>
                 Spawn(path, args, s => (outputTag, s), s => (errorTag, s));

    public static (int, ICollection<T>)
        Spawn<T>(string path, string[] args,
                 Func<string, T> outputSelector,
                 Func<string, T> errorSelector)
    {
        var psi = new ProcessStartInfo
        {
            UseShellExecute         = false,
            CreateNoWindow          = true,
            RedirectStandardOutput  = true,
            RedirectStandardError   = true,
            FileName                = path,
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = System.Diagnostics.Process.Start(psi);

        var results = new List<T>();
        process.ErrorDataReceived  += CreateDataReceivedEventHandler(errorSelector,  results.Add);
        process.OutputDataReceived += CreateDataReceivedEventHandler(outputSelector, results.Add);

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();

        return (process.ExitCode, results);

        static DataReceivedEventHandler CreateDataReceivedEventHandler(Func<string, T> selector,
                                                                       Action<T> appender) =>
            (_, args) =>
            {
                if (args.Data is string data)
                    appender(selector(data));
            };
    }
}
