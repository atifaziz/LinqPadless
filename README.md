# LINQPadless

[![NuGet][nuget-badge]][nuget-pkg]

LINQPadless compiles and runs [LINQPad] query files as stand-alone .NET Core
applications without the need for LINQPad.

The compilation is cached and re-used until the source query file changes.

The LINQPad query file can be run on any platform where .NET Core is
supported however it is the responsibility of the query author to ensure that
the code and packages referenced are compatible with .NET Core and the
execution platform.


## Usage Examples

Compile and run a single LINQPad query file in the current directory:

    lpless Foobar.linq

Compile but don't run:

    lpless -x Foobar.linq

Force a re-compilation before running even if the LINQPad query file has not
changed since the last run:

    lpless -f Foobar.linq

For more information, see help:

    lpless -h


## Motivation

> Why does LINQPadless exist?

LINQPad is an excellent alternative to Visual Studio when you want to script
some code but don't want all the ceremony of a Visual Studio solution or
project. You can use NuGet packages, get the same experience as IntelliSense,
even debug through your code and all the while maintaining a single source
file. What's there not to love about it? However, when you want to ship that
code to someone or automate it, you are tied to LINQPad when that dependency
is not necessary. That's where `lpless` comes in. It turns your LINQ Query
file into a C# script or an executable that you can then run without LINQPad.

> What's different from `lprun`?

[`lprun`][lprun] is a good solution when you need 100% compatibility and
parity with LINQPad features at _run-time_. On the other hand, when all you
are doing is using [LINQPad as a lightweight IDE][lpide] to script some task
that doesn't need its bells and whistles then turning those queries into
compiled executables enables them to be shipped and run without LINQPad.


## Limitations

Requires .NET SDK 6+ for execution.

LINQPad Query files must be either C# Statements, Expression or Program.

LINQPad-specific methods like `Dump` and those on its `Util` class will
cause compilation errors.

In [loaded (`#load`) queries][linqref]:

- the `Hijack` hook method is not supported.
- only an absolute path and a path relative to where the query is saved are
  supported in the `#load` directive.


[nuget-badge]: https://img.shields.io/nuget/v/LinqPadless.svg
[nuget-pkg]: https://www.nuget.org/packages/LinqPadless
[LINQPad]: http://www.linqpad.net/
[lpide]: https://www.linqpad.net/CodeSnippetIDE.aspx
[lprun]: https://www.linqpad.net/lprun.aspx
[linqref]: https://www.linqpad.net/LinqReference.aspx
