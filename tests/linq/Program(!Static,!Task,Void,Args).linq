<Query Kind="Program" />

void Main(string[] args)
{
    Console.WriteLine(GetType().FullName);
    Console.WriteLine(Greeting.Message);
    Console.WriteLine(string.Join(",", args));
}

static class Greeting
{
    public static string Message => "Hello, World!";
}

//< 0
//| UserQuery
//| Hello, World!
//| foo,bar,baz
