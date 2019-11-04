<Query Kind="Statements">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

Console.WriteLine(await Task.FromResult("Hello, World!"));

//< 0
//| Hello, World!
