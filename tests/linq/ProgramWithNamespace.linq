<Query Kind="Program" />

void Main()
{
    Console.WriteLine(new Foo.Bar().Message);
}

namespace Foo
{
    class Bar
    {
        public string Message = "Hello, world";
    }
}

//< 0
//| Hello, world
