<Query Kind="Statements">
  <NuGetReference Version="3.0.0">morelinq</NuGetReference>
</Query>

#load ".\lib\MoreLinq-3.2.0.linq"

Console.WriteLine(typeof(MoreLinq.MoreEnumerable).Assembly.GetName().Version);

//< 0
//| 3.0.0.0
//| 3.0.0.0