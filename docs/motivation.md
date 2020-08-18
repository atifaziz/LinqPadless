# Motivation


## Why does LINQPadless exist?

LINQPad is an excellent alternative to Visual Studio when you want to script
some code but don't want all the ceremony of a Visual Studio solution or
project. You can use NuGet packages, get the same experience as IntelliSense,
even debug through your code and all the while maintaining a single source
file. What's there not to love about it? However, when you want to ship that
code to someone or automate it, you are tied to LINQPad when that dependency
is not necessary. That's where `lpless` comes in. It turns your LINQ Query
file into a C# script or an executable that you can then run without LINQPad.


## What's different from `lprun`?

[`lprun`][lprun] is a good solution when you need 100% compatibility and
parity with LINQPad features at _run-time_. On the other hand, when all you
are doing is using [LINQPad as a lightweight IDE][lpide] to script some task
that doesn't need its bells and whistles then turning those queries into
compiled executables enables them be shipped and run without LINQPad.


[lprun]: https://www.linqpad.net/lprun.aspx
[lpide]: https://www.linqpad.net/CodeSnippetIDE.aspx
