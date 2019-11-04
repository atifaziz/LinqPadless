<Query Kind="Program">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

async Task Main()
{
    Console.WriteLine(GetType().FullName);
    Console.WriteLine(await Task.FromResult(Greeting.Message));
}

static class Greeting
{
    public static string Message => "Hello, World!";
}

//< 0
//| UserQuery
//| Hello, World!
