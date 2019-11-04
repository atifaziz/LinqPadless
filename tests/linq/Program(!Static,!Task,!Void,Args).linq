<Query Kind="Program" />

int Main(string[] args)
{
    Console.WriteLine(GetType().FullName);
    Console.WriteLine(Greeting.Message);
    return args.Length;
}

static class Greeting
{
    public static string Message => "Hello, World!";
}

//< 3
//| UserQuery
//| Hello, World!
