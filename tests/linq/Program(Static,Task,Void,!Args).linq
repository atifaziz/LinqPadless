<Query Kind="Program">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

static async Task Main()
{
    Console.WriteLine(await Task.FromResult(Greeting.Message));
}

static class Greeting
{
    public static string Message => "Hello, World!";
}

//< 0
//| Hello, World!
