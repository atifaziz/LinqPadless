<Query Kind="Program">
  <Namespace>MoreEnumerable = MoreLinq.MoreEnumerable</Namespace>
  <Namespace>static MoreLinq.Extensions.ToDelimitedStringExtension</Namespace>
</Query>

#load ".\lib\Program.linq"
#load ".\lib\LineNumber.linq"

void Main() =>
    Console.WriteLine(string.Join(Environment.NewLine,
        "Hello, World!",
        ++privateField,
        PrivateMethod(),
        "Extension".Extension(),
        typeof(Nested).FullName,
        typeof(Namespace.UserQuery).FullName,
        MoreEnumerable.Sequence(10, 0).ToDelimitedString(", "),
        $"Caller @ {GetCallerLocation()}",
        $"Called @ {GetCalledLocation()}"));

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
//| Caller @ ArrowProgramLoadsProgram.linq:13
//| Called @ LineNumber.linq:2
//| OnFinish1
//| OnFinish