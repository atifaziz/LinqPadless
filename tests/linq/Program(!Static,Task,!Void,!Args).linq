<Query Kind="Program">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

async Task<int> Main()
{
    Console.WriteLine(GetType().FullName);
    Console.WriteLine(Greeting.Message);
    return await Task.FromResult(42);
}

static class Greeting
{
    public static string Message => "Hello, World!";
}

//< 42
//| UserQuery
//| Hello, World!
