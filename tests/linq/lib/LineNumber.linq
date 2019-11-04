<Query Kind="Program">
  <Namespace>System.Runtime.CompilerServices</Namespace>
</Query>

int GetCallerLineNumber([CallerLineNumber]int line = 0) => line;
int GetCalledLineNumber() => GetCallerLineNumber();

void Main()
{
    Console.WriteLine(GetCallerLineNumber());
    Console.WriteLine(GetCalledLineNumber());
}
