<Query Kind="Program" />

int Main()
{
    Console.WriteLine(GetType().FullName);
    Console.WriteLine(Greeting.Message);
    return 42;
}

static class Greeting
{
    public static string Message => "Hello, World!";
}

//< 42
//| UserQuery
//| Hello, World!
