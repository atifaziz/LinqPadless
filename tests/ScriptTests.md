# Script Tests


## Query Kinds


### Expression

Suppose:

- kind is expression

Source:

```
DateTime.Now
```

Expected:

```
<Query Kind="Expression" />

DateTime.Now
```


### Statements

Suppose:

- kind is statements

Source:

```
Console.WriteLine(DateTime.Now);
```

Expected:

```
<Query Kind="Statements" />

Console.WriteLine(DateTime.Now);
```


### Program

Suppose:

- kind is program

Source:

```
void Main()
{
    Console.WriteLine(DateTime.Now);
}
```

Expected:

```
<Query Kind="Program" />

void Main()
{
    Console.WriteLine(DateTime.Now);
}
```


## Single import

Source:

```
using System.Net.Http;

DateTime.Now
```

Expected:

```
<Query Kind="Expression">
  <Namespace>System.Net.Http</Namespace>
</Query>

DateTime.Now
```


## Multiple imports

Source:

```
using System.Net.Http;
using System.Security.Cryptography;

DateTime.Now
```

Expected:

```
<Query Kind="Expression">
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Security.Cryptography</Namespace>
</Query>

DateTime.Now
```


## Mixed types of imports

Source:

```
using System.Net.Http;
using System.Security.Cryptography;
using static System.Math;
using Int = System.Int32;

DateTime.Now
```

Expected:

```
<Query Kind="Expression">
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Security.Cryptography</Namespace>
  <Namespace>static System.Math</Namespace>
  <Namespace>Int = System.Int32</Namespace>
</Query>

DateTime.Now
```


## Imports without semi-colon termination

Source:

```
using System.Net.Http
using System.Security.Cryptography
using static System.Math
using Int = System.Int32
DateTime.Now
```

Expected:

```
<Query Kind="Expression">
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Security.Cryptography</Namespace>
  <Namespace>static System.Math</Namespace>
  <Namespace>Int = System.Int32</Namespace>
</Query>

DateTime.Now
```


## Spaced-out imports

Whitespace between imports is discarded.

Source:

```
using System.Net.Http

using System.Security.Cryptography

using static System.Math

using Int = System.Int32

DateTime.Now
```

Expected:

```
<Query Kind="Expression">
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Security.Cryptography</Namespace>
  <Namespace>static System.Math</Namespace>
  <Namespace>Int = System.Int32</Namespace>
</Query>

DateTime.Now
```


## Commented-out imports

Source:

```
using System.Net.Http
//using System.Security.Cryptography
using static System.Math
//using Int = System.Int32

DateTime.Now
```

Expected:

```
<Query Kind="Expression">
  <Namespace>System.Net.Http</Namespace>
  <Namespace>static System.Math</Namespace>
</Query>

//using System.Security.Cryptography
//using Int = System.Int32

DateTime.Now
```


## Comments on same line as import

Source:

```
using System.Net.Http // this is a comment
using static System.Math

DateTime.Now
```

Expected:

```
<Query Kind="Expression">
  <Namespace>System.Net.Http</Namespace>
  <Namespace>static System.Math</Namespace>
</Query>

DateTime.Now
```


## NuGet references


### With version

Source:

```
#r "nuget: System.Reactive, 4.3.2"
using System.Reactive.Linq;

Observable.Range(1,10)
```

Expected:

```
<Query Kind="Expression">
  <NuGetReference Version="4.3.2">System.Reactive</NuGetReference>
  <Namespace>System.Reactive.Linq</Namespace>
</Query>

Observable.Range(1,10)
```


### Cannot follow imports

Source:

```
using System.Reactive.Linq;
#r "nuget: System.Reactive"

Observable.Range(1,10)
```

Error expected:

```
The reference on line 2 must precede the first import: #r "nuget: System.Reactive"
```

### Commented-out

Suppose:

- kind is expression

Source:

```
//#r "nuget: System.Reactive"
using System.Reactive.Linq;
//#r "nuget: System.Reactive"

Observable.Range(1,10)
```

Expected:

```
<Query Kind="Expression">
  <Namespace>System.Reactive.Linq</Namespace>
</Query>

//#r "nuget: System.Reactive"
//#r "nuget: System.Reactive"

Observable.Range(1,10)
```
