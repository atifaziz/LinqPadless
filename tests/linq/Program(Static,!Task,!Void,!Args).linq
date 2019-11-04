<Query Kind="Program" />

static int Main()
{
    Console.WriteLine(Greeting.Message);
    return 42;
}

static class Greeting
{
    public static string Message => "Hello, World!";
}

//< 42
//| Hello, World!
