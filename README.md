# LINQPadless

LINQPadless compiles [LINQPad][linqpad] query files into stand-alone
[C# scripts (csx)][csx] so they can run outside and independent of LINQPad.

The compiler emits a C# script file in the same directory and with the same
file name as the original `.linq` except it bears the `.csx` extension. It also
creates a Windows batch file alongside that does the following:

- Checks that referenced NuGet packages are installed.
- Installs missing NuGet packages.
- Invoke the C# script using `csi.exe` and passes any remaining arguments.

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

Compile all LINQPad query files starting with `Foo`

    lpless -r C:\LINQPad\Queries\Foo*.linq

Watch particular files in a directory and re-compile them on changes:

    lpless -w C:\LINQPad\Queries\Foo*.linq

For more information, see help:

    lpless -h

## Limitations

LINQPad Query files must be C# statements.

Extension methods are not supported in C# scripts.

LINQPad-specified methods like `Dump` and those on its `Util` class will
cause compilation errors when the compiled C# script is executed. This issue
may be addressed in the future via some sort of a bridge library.


[linqpad]: http://www.linqpad.net/
[csx]: https://msdn.microsoft.com/en-us/magazine/mt614271.aspx
