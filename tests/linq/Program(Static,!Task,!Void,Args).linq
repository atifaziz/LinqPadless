<Query Kind="Program" />

static int Main(string[] args)
{
    Console.WriteLine(Greeting.Message);
    Console.WriteLine(string.Join(",", args));
    return 42;
}

static class Greeting
{
    public static string Message => "Hello, World!";
}

//< 42
//| Hello, World!
//| foo,bar,baz
