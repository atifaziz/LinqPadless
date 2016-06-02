# LINQPadless

LINQPadless compiles [LINQPad][linqpad] query files into stand-alone
[C# scripts (csx)][csx] so they can run outside and independent of LINQPad.

The LINQPadless compiler takes a LINQPad query file (`.linq` extension) path
as its sole argument:

    lpless QUERY-PATH

The compiler emits a C# script file in the same directory and with the same
file name as the original except it bears the `.csx` extension. It also
creates a Windows batch file alongside that can be used to invoke the C#
script using `csi.exe`.

LINQPadless currently only supports C#.


[linqpad]: http://www.linqpad.net/
[csx]: https://msdn.microsoft.com/en-us/magazine/mt614271.aspx
