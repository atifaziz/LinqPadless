# Introduction

[![NuGet][nuget-badge]][nuget-pkg]

LINQPadless compiles and runs [LINQPad] query files as stand-alone .NET Core
applications, without the need for LINQPad.

The LINQPad query file can be run on any platform where .NET Core is
supported however it is the responsibility of the query author to ensure that
the code and packages referenced are compatible with .NET Core as well as the
execution platform.

LINQPadless is designed to feel like a scripting environment. All you need
to maintain are your LINQPad query files. At time of execution, LINQPadless
turns them into full programs, then compiles and executes them. The compilation
is cached and re-used until the source query file changes so while the first
execution may seem slow, subsequent ones run fast.

The compilation requires an installation of .NET Core SDK 2.1 or a later
version.

[nuget-badge]: https://img.shields.io/nuget/v/LinqPadless.svg
[nuget-pkg]: https://www.nuget.org/packages/LinqPadless
[LINQPad]: http://www.linqpad.net/
[lpide]: https://www.linqpad.net/CodeSnippetIDE.aspx
[lprun]: https://www.linqpad.net/lprun.aspx
