<Query Kind="Program" />

static void Main(string[] args)
{
    Console.WriteLine(Greeting.Message);
    Console.WriteLine(string.Join(",", args));
}

static class Greeting
{
    public static string Message => "Hello, World!";
}

//< 0
//| Hello, World!
//| foo,bar,baz
