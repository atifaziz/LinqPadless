using System;
using System.IO;
using System.Runtime.CompilerServices;

static class Util
{
    public static T Dump<T>(this T value,
        [CallerFilePath] string sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Logger.Log($"{Path.GetFileName(sourceFilePath)}@{sourceLineNumber}:{value}", ConsoleColor.DarkBlue);
        return value;
    }
}
