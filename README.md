# LINQPadless

LINQPadless compiles [LINQPad][linqpad] query files into stand-alone
[C# scripts (csx)][csx] so that they can be run outside and independent of
LINQPad.

The compiler emits a C# script file in the same directory and with the same
file name as the original except it bears the `.csx` extension. It also
creates a Windows batch file alongside that does the following:

- Checks that referenced NuGet packages are installed.
- Installs missing NuGet packages.
- Sets `LINQPADLESS` environment variable to the compiler version.
- Invokes the C# script using `csi.exe` and passes any remaining arguments.


## Usage Examples

Compile a single LINQPad query file in the current directory:

    lpless Foobar.linq

Compile all LINQPad query files:

    lpless *.linq

Compile all LINQPad query files in a specific directory:

    lpless C:\LINQPad\Queries\*.linq

Compile all LINQPad query files in a specific directory and sub-directories
within:

    lpless -r C:\LINQPad\Queries\*.linq

Compile all LINQPad query files starting with `Foo`:

    lpless -r C:\LINQPad\Queries\Foo*.linq

Compile (incremental) outdated files only:

    lpless -i C:\LINQPad\Queries\Foo*.linq

Watch particular files in a directory and re-compile outdated ones on changes:

    lpless -w C:\LINQPad\Queries\Foo*.linq

Compile `Foo.linq` using the `Bar` package and importing the `Baz` namespace
(both in addition to packages and namespaces referenced in `Foo.linq`):

    lpless --ref Bar --imp Baz Foo.linq

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
file into a C# script that you can then run without LINQPad.

> What's different from `lprun`?

[`lprun`][lprun] is a good solution when you need 100% compatibility and
parity with LINQPad features at _run-time_. On the other hand, when all you
are doing is using [LINQPad as a lightweight IDE][lpide] to script some task
that doesn't need its bells and whistles then turning those queries into C#
scripts (`.csx`) enables them be shipped and run without LINQPad.


## Limitations

LINQPad Query files must be either C# Statements, Expression or Program. In
the case of a C# Program query, a `Main` declared to be asynchronous is not
awaited and therefore may not execute fully.

Extension methods are not supported in C# scripts.

LINQPad-specified methods like `Dump` and those on its `Util` class will
cause compilation errors when the compiled C# script is executed. This issue
may be addressed in the future via some sort of a bridge library.


[linqpad]: http://www.linqpad.net/
[csx]: https://msdn.microsoft.com/en-us/magazine/mt614271.aspx
[lpide]: https://www.linqpad.net/CodeSnippetIDE.aspx
[lprun]: https://www.linqpad.net/lprun.aspx