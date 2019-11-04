<Query Kind="Expression">
  <Namespace>MoreEnumerable = MoreLinq.MoreEnumerable</Namespace>
  <Namespace>static MoreLinq.Extensions.ToDelimitedStringExtension</Namespace>
</Query>

#load ".\lib\Program.linq"
#load ".\lib\LineNumber.linq"

string.Join(Environment.NewLine,
    ++privateField,
    PrivateMethod(),
    "Extension".Extension(),
    typeof(Nested).FullName,
    typeof(Namespace.UserQuery).FullName,
    MoreEnumerable.Sequence(10, 0).ToDelimitedString(", "),
    $"Caller line #{GetCallerLineNumber()}",
    $"Called line #{GetCalledLineNumber()}")

//< 0
//| OnInit1
//| OnStart1
//| 11
//| PrivateMethod
//| Extension
//| UserQuery+Nested
//| Namespace.UserQuery
//| 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0
//| Caller line #11
//| Called line #2
//| OnFinish1
