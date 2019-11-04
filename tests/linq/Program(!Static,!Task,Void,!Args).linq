<Query Kind="Program" />

void Main()
{
    Console.WriteLine(GetType().FullName);
    Console.WriteLine(Greeting.Message);
}

static class Greeting
{
    public static string Message => "Hello, World!";
}

//< 0
//| UserQuery
//| Hello, World!
