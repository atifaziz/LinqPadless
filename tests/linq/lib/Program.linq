<Query Kind="Program">
  <NuGetReference Version="3.2.0">morelinq</NuGetReference>
  <Namespace>MoreLinq</Namespace>
  <RemoveNamespace>System</RemoveNamespace>
  <RemoveNamespace>System.Collections</RemoveNamespace>
  <RemoveNamespace>System.Collections.Generic</RemoveNamespace>
  <RemoveNamespace>System.Data</RemoveNamespace>
  <RemoveNamespace>System.Diagnostics</RemoveNamespace>
  <RemoveNamespace>System.IO</RemoveNamespace>
  <RemoveNamespace>System.Linq</RemoveNamespace>
  <RemoveNamespace>System.Linq.Expressions</RemoveNamespace>
  <RemoveNamespace>System.Text</RemoveNamespace>
  <RemoveNamespace>System.Text.RegularExpressions</RemoveNamespace>
  <RemoveNamespace>System.Threading</RemoveNamespace>
  <RemoveNamespace>System.Transactions</RemoveNamespace>
  <RemoveNamespace>System.Xml</RemoveNamespace>
  <RemoveNamespace>System.Xml.Linq</RemoveNamespace>
  <RemoveNamespace>System.Xml.XPath</RemoveNamespace>
</Query>

void Main()
{
    System.Console.WriteLine("This should not run when included in another program.");
}

string PrivateMethod() => nameof(PrivateMethod);
int privateField = 10;

static class Extensions
{
    public static T Extension<T>(this T _) => _;
}

sealed class Nested {}

namespace Namespace
{
    class UserQuery{}
}

void OnInit()   => System.Console.WriteLine(MethodBase.GetCurrentMethod().Name);
void OnStart()  => System.Console.WriteLine(MethodBase.GetCurrentMethod().Name);
void OnFinish() => System.Console.WriteLine(MethodBase.GetCurrentMethod().Name);
