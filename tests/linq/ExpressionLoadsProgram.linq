<Query Kind="Expression">
  <Namespace>static MoreLinq.Extensions.ToDelimitedStringExtension</Namespace>
</Query>

#load ".\lib\Program.linq"

string.Join(Environment.NewLine,
    ++privateField,
    PrivateMethod(),
    "Extension".Extension(),
    typeof(Nested).FullName,
    typeof(Namespace.UserQuery).FullName,
    MoreLinq.MoreEnumerable.Sequence(10, 0).ToDelimitedString(", "))

//< 0
//| OnInit1
//| OnStart1
//| 11
//| PrivateMethod
//| Extension
//| UserQuery+Nested
//| Namespace.UserQuery
//| 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0
//| OnFinish1