<Query Kind="Program">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

static async Task Main(string[] args)
{
    Console.WriteLine(await Task.FromResult(Greeting.Message));
    Console.WriteLine(string.Join(",", args));
}

static class Greeting
{
    public static string Message => "Hello, World!";
}

//< 0
//| Hello, World!
//| foo,bar,baz
