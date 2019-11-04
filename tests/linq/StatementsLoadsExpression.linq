<Query Kind="Statements">
  <RemoveNamespace>System</RemoveNamespace>
  <RemoveNamespace>System.Collections</RemoveNamespace>
  <RemoveNamespace>System.Collections.Generic</RemoveNamespace>
  <RemoveNamespace>System.Data</RemoveNamespace>
  <RemoveNamespace>System.Diagnostics</RemoveNamespace>
  <RemoveNamespace>System.IO</RemoveNamespace>
  <RemoveNamespace>System.Linq</RemoveNamespace>
  <RemoveNamespace>System.Linq.Expressions</RemoveNamespace>
  <RemoveNamespace>System.Reflection</RemoveNamespace>
  <RemoveNamespace>System.Text</RemoveNamespace>
  <RemoveNamespace>System.Text.RegularExpressions</RemoveNamespace>
  <RemoveNamespace>System.Threading</RemoveNamespace>
  <RemoveNamespace>System.Transactions</RemoveNamespace>
  <RemoveNamespace>System.Xml</RemoveNamespace>
  <RemoveNamespace>System.Xml.Linq</RemoveNamespace>
  <RemoveNamespace>System.Xml.XPath</RemoveNamespace>
</Query>

#load ".\lib\Expression.linq"
#load ".\lib\LineNumber.linq"

Console.WriteLine(
    Enumerable
        .Repeat("This is Statements loading Expression", 3)
        .Index(1)
        .Select(e => $"{e.Key}. {e.Value}")
        .ToDelimitedString(Environment.NewLine));

Console.WriteLine($"Caller line #{GetCallerLineNumber()}");
Console.WriteLine($"Called line #{GetCalledLineNumber()}");

//< 0
//| Greetings from Expression!
//| 1. This is Statements loading Expression
//| 2. This is Statements loading Expression
//| 3. This is Statements loading Expression
//| Caller line #11
//| Called line #2