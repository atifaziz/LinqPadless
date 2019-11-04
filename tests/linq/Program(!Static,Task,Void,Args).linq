<Query Kind="Program">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

async Task Main(string[] args)
{
    Console.WriteLine(GetType().FullName);
    Console.WriteLine(await Task.FromResult(Greeting.Message));
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
