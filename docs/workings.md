# Usage

LINQPadless works by converting LINQPad query files (`*.linq`) into
full-fledged C# programs that then get compiled and executed. To compile, it
requires the [.NET Core SDK][dotnet].

The compilation of a query is cached so that subsequent runs are quick by
skipping the conversion and compilation steps.

A program _template_ is used to convert a LINQPad query file (`*.linq`) into
a full-fledged C# program. The template is just a normal C# project with one or
more source files where placeholders and conditional compilation drive the
final code that will run.

When LINQPadless is given a LINQPad query file, it first determines if a
previous compilation exists in the cache that can be re-used. It starts by
computing a hash of the query file's content.

 The cache is
located in `.lpless/cache`

1. Generate a hash with the content of the LINQPad query file.
2. Look-up the hash in the cache. If a compilation exists under the hash
   then execute it and skip all subsequent steps.
3. Locate a C# project template and use it to convert the code of the
   LINQPad query file into a full-fledged C# program.
4. Compile the project created from the template in the previous step and
   store the compilation in the cache under the hash computed in step 1.
5. Execute the compiled binary in the cache.


  [dotnet]: https://dot.net
