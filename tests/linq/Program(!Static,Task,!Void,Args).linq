<Query Kind="Program">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

async Task<int> Main(string[] args)
{
    Console.WriteLine(GetType().FullName);
    Console.WriteLine(Greeting.Message);
    Console.WriteLine(string.Join(",", args));
    return await Task.FromResult(42);
}

static class Greeting
{
    public static string Message => "Hello, World!";
}

//< 42
//| UserQuery
//| Hello, World!
//| foo,bar,baz
