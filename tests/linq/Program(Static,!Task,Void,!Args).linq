<Query Kind="Program" />

static void Main()
{
    Console.WriteLine(Greeting.Message);
}
static class Greeting
{
    public static string Message => "Hello, World!";
}

//< 0
//| Hello, World!
