# LINQPadless

LINQPadless compiles [LINQPad][linqpad] query files into stand-alone
[C# scripts (csx)][csx] so they can run outside and independent of LINQPad.


The LINQPadless compiler takes a LINQPad query file (`.linq` extension) path
as its sole argument:

    lpless SCRIPT-PATH

The compiler emits a C# script file with the same file name and in the same
directory except bearing the `.csx` extension. It also creates a Windows batch
script file alongside to invoke the C# script using `csi.exe`.

LINQPadless currently only supports C#.


[linqpad]: http://www.linqpad.net/
[csx]: https://msdn.microsoft.com/en-us/magazine/mt614271.aspx
