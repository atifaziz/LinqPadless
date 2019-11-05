<Query Kind="Program">
  <Namespace>System.Runtime.CompilerServices</Namespace>
</Query>

string GetCallerLocation([CallerFilePath]string path = null, [CallerLineNumber]int line = 0) => $"{Path.GetFileName(path)}:{line}";
string GetCalledLocation() => GetCallerLocation();

void Main()
{
    Console.WriteLine(GetCallerLocation());
    Console.WriteLine(GetCalledLocation());
}