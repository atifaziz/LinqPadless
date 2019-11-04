<Query Kind="Expression">
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

#load ".\lib\Statements.linq"
#load ".\lib\LineNumber.linq"

Enumerable
    .Repeat("This is Expression loading Statements", 3)
    .Index(1)
    .Select(e => $"{e.Key}. {e.Value}")
    .Append($"Caller line #{GetCallerLineNumber()}")
    .Append($"Called line #{GetCalledLineNumber()}")
    .ToDelimitedString(Environment.NewLine)

//< 0
//| 1. This is Expression loading Statements
//| 2. This is Expression loading Statements
//| 3. This is Expression loading Statements
//| Caller line #8
//| Called line #2
