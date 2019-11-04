<Query Kind="Program">
  <Namespace>System.Runtime.CompilerServices</Namespace>
</Query>

static (int, int) Foo([CallerLineNumber]int line = 0) =>
    (line, Bar());

static int Bar([CallerLineNumber]int line = 0) => line;

void Main() =>
    Console.WriteLine(Foo());

//< 0
//| (7, 2)
