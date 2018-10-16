using System;

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