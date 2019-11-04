<Query Kind="Statements">
  <Namespace>static MoreLinq.Extensions.ToDelimitedStringExtension</Namespace>
</Query>

#load ".\lib\Program.linq"

Console.WriteLine("Hello, World!");
Console.WriteLine(++privateField);
Console.WriteLine(PrivateMethod());
Console.WriteLine("Extension".Extension());
Console.WriteLine(typeof(Nested).FullName);
Console.WriteLine(typeof(Namespace.UserQuery).FullName);
Console.WriteLine(MoreLinq.MoreEnumerable.Sequence(10, 0).ToDelimitedString(", "));

//< 0
//| OnInit1
//| OnStart1
//| Hello, World!
//| 11
//| PrivateMethod
//| Extension
//| UserQuery+Nested
//| Namespace.UserQuery
//| 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0
//| OnFinish1
