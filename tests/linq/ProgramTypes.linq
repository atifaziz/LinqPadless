<Query Kind="Program">
  <Namespace>Namespace</Namespace>
</Query>

void Main()
{
    Print(NestedClass.StaticMethod      );
    Print(NestedStruct.StaticMethod     );
    Print(NestedStaticClass.StaticMethod);
    Print(default(object).Extension     );
    Print(Extensions.StaticMethod       );

    Print(Namespace.Class.StaticMethod      );
    Print(Namespace.Struct.StaticMethod     );
    Print(Namespace.StaticClass.StaticMethod);
    Print(default(object).Extension2        );
    Print(Namespace.Extensions.StaticMethod );
}

static void Print(Action a) =>
    Console.WriteLine($"{a.Method.DeclaringType.FullName}::{a.Method.Name}");

struct NestedStruct
{
    public static void StaticMethod() {}
}

class NestedClass
{
    public static void StaticMethod() {}
}

static class NestedStaticClass
{
    public static void StaticMethod() {}
}

static class Extensions
{
    public static void StaticMethod() {}
    public static void Extension<T>(this T _) {}
}

namespace Namespace
{
    struct Struct
    {
        public static void StaticMethod() {}
    }

    class Class
    {
        public static void StaticMethod() {}
    }

    static class StaticClass
    {
        public static void StaticMethod() {}
    }

    static class Extensions
    {
        public static void StaticMethod() {}
        public static void Extension2<T>(this T _) {}
    }
}

//< 0
//| UserQuery+NestedClass::StaticMethod
//| UserQuery+NestedStruct::StaticMethod
//| UserQuery+NestedStaticClass::StaticMethod
//| Extensions::Extension
//| Extensions::StaticMethod
//| Namespace.Class::StaticMethod
//| Namespace.Struct::StaticMethod
//| Namespace.StaticClass::StaticMethod
//| Namespace.Extensions::Extension2
//| Namespace.Extensions::StaticMethod
