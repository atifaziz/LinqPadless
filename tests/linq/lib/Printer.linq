<Query Kind="Program">
  <Namespace>System.Globalization</Namespace>
</Query>

void Main() {}

[AttributeUsage(AttributeTargets.Method)]
sealed class QueryExpressionPrinterAttribute : Attribute {}

[QueryExpressionPrinterAttribute]
void Print(object value)
{
    Console.WriteLine(Convert.ToString(value, CultureInfo.InvariantCulture));
}

[QueryExpressionPrinterAttribute]
void Print(string str)
{
    Console.WriteLine(str.ToUpperInvariant());
}

[QueryExpressionPrinterAttribute]
void Print<T>(IEnumerable<T> source)
{
    foreach (var item in source)
        Console.WriteLine(item);
}
