<Query Kind="Program">
  <Namespace>MoreEnumerable = MoreLinq.MoreEnumerable</Namespace>
  <Namespace>static MoreLinq.Extensions.ToDelimitedStringExtension</Namespace>
</Query>

#load ".\lib\Program.linq"
#load ".\lib\LineNumber.linq"

void Main()
{
    Console.WriteLine("Hello, World!");
    Console.WriteLine(++privateField);
    Console.WriteLine(PrivateMethod());
    Console.WriteLine("Extension".Extension());
    Console.WriteLine(typeof(Nested).FullName);
    Console.WriteLine(typeof(Namespace.UserQuery).FullName);
    Console.WriteLine(MoreEnumerable.Sequence(10, 0).ToDelimitedString(", "));
    Console.WriteLine($"Caller @ {GetCallerLocation()}");
    Console.WriteLine($"Called @ {GetCalledLocation()}");
}

void OnInit()   => Console.WriteLine(MethodBase.GetCurrentMethod().Name);
void OnStart()  => Console.WriteLine(MethodBase.GetCurrentMethod().Name);
void OnFinish() => Console.WriteLine(MethodBase.GetCurrentMethod().Name);

//< 0
//| OnInit
//| OnInit1
//| OnStart1
//| OnStart
//| Hello, World!
//| 11
//| PrivateMethod
//| Extension
//| UserQuery+Nested
//| Namespace.UserQuery
//| 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0
//| Caller @ ProgramLoadsProgram.linq:13
//| Called @ LineNumber.linq:2
//| OnFinish1
//| OnFinish