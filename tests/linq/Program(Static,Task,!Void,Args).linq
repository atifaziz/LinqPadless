<Query Kind="Program">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

static async Task<int> Main(string[] args)
{
    Console.WriteLine(Greeting.Message);
    Console.WriteLine(string.Join(",", args));
    return await Task.FromResult(42);
}

static class Greeting
{
    public static string Message => "Hello, World!";
}

//< 42
//| Hello, World!
//| foo,bar,baz
