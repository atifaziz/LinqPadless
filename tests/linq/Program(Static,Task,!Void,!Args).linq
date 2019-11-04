<Query Kind="Program">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

static async Task<int> Main()
{
    Console.WriteLine(Greeting.Message);
    return await Task.FromResult(42);
}

static class Greeting
{
    public static string Message => "Hello, World!";
}

//< 42
//| Hello, World!
