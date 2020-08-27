# Tutorial

The simplest way to take LINQPadless for a spin is to try it in a [Docker]
container. Assuming you have Docker installed for your operating system,
start by cloning the [project's repository][repo]:

    git clone https://github.com/atifaziz/LinqPadless.git LinqPadless

Next, make the working directory of the clone the current working directory of
your shell. In most shells, you can do this with:

    cd LinqPadless

Build the Docker image as follows (it will be named/tagged `lpless`):

    docker build --rm -t lpless .

Once the image is built, run a container as follows:

    docker run --rm -it --entrypoint /bin/sh lpless

The `--entrypoint /bin/sh` is necessary to _enter_ the shell of the container
image (based on [Alpine Linux]) so you can explore running LINQPadless.
Without changing the entry-point of the image, LINQPadless will just run,
display its help and exit.

Once the container is running, you will see a shell prompt with
`/scripts/linq` as your current directory:

    /scripts/linq # █

For the purpose of this tutorial, █ represents your cursor.

Copy and paste the following on the shell prompt to create a tiny LINQPad
query file that has the C# expression `DateTime.Now`:

    cat > Now.linq <<EOF
    <Query Kind="Expression" />

    DateTime.Now
    EOF

Everything between `<<EOF` and `EOF` will become the content of the file
`Now.linq`. If you have never opened a LINQPad query file in a plain text
editor then the above may seem strange. In short, LINQPad query files are
plain text files that have the following structure:

- a header that is an XML element containing LINQPad meta data
- a blank line that separates the header from the remaining content
- body of code (expression, statements or program)

The `<Query Kind="Expression" />` that we placed in the header of the
`Now.linq` file says that the code `DateTime.Now` is a C# expression.

Your shell session should now be looking as follows:

    /scripts/linq # cat > Now.linq <<EOF
    > <Query Kind="Expression" />
    >
    > DateTime.Now
    > EOF
    /scripts/linq # █

Let's run it by typing `lpless Now.linq` at the shell prompt. After a few
seconds, you should see the current time appear as shown below:

    /scripts/linq # lpless Now.linq
    8/25/2020 4:48:42 PM
    /scripts/linq # █

If you run it as second time, you should notice that it executes almost
instantly. LINQPadless caches the results of previous compilations and
re-uses them if there have been no changes to the source files. In fact,
let's time the execution with `time lpless Now.linq`:

    /scripts/linq # time lpless Now.linq
    8/25/2020 4:50:14 PM
    real    0m 0.35s
    user    0m 0.33s
    sys     0m 0.04s

You can see that it executed in a fraction of a second. We can force a
re-compilation even if a cached version is available by adding the `--force`
flag (or just `-f` for short) _before_ the query file name.

    /scripts/linq # time lpless --force Now.linq
    8/25/2020 4:50:55 PM
    real    0m 2.55s
    user    0m 2.44s
    sys     0m 0.30s

Notice how it took substantially longer time to run. If you copy the file and
run the copy, it will also run quickly:

    /scripts/linq # cp Now.linq Now2.linq
    /scripts/linq # time lpless Now2.linq
    8/25/2020 4:57:46 PM
    real    0m 0.41s
    user    0m 0.38s
    sys     0m 0.04s

LINQPadless is smart enough to re-use a previous compilation across sources
if they match. Internally, it computes a digest/hash of the file content and
uses that to look-up a compilation in its on-disk cache. Since `Now.linq` and
`Now2.linq` are copies of each other, running the copy is just as fast the
original.

Let's explore a LINQPad query file with C# statements:

    cat > Statements.linq <<EOF
    <Query Kind="Statements" />

    Console.WriteLine(DateTime.Now);
    EOF

Run it with `lpless Statements.linq`:

    /scripts/linq # cat > Statements.linq <<EOF
    > <Query Kind="Statements" />
    >
    > Console.WriteLine(DateTime.Now);
    > EOF
    /scripts/linq # lpless Statements.linq
    8/25/2020 5:04:26 PM
    /scripts/linq # █

It prints the current date and time like the first example, but since the
XML header `<Query Kind="Statements" />` says the file contains statements,
the date and time had to be printed to the console as a regular
C# statement using:

``` c#
Console.WriteLine(DateTime.Now);
```

Let's append another statement to the same file that prints `Hello!`:

    /scripts/linq # echo 'Console.WriteLine("Hello!");' >> Statements.linq
    /scripts/linq # lpless Statements.linq
    8/25/2020 5:08:13 PM
    Hello!

Since `Statements.linq` was modified, you will notice the compilation delay
when `lpless Statements.linq` executes.

The final type of LINQPad query file supported by LINQPadless is a C#
program, but for this, let's try something more complicated. A slightly
modified version of the [dotnet-cowsay] project has been shared at the
following URL:

> [http://share.linqpad.net/9bfa4d.linq](http://share.linqpad.net/9bfa4d.linq).

You open download and open it up in LINQPad if you want to take a peek inside,
even run it! The program selects a random blog article from [Discover .NET]
and then displays an ASCII cow with a speech bubble containing the selected
blog title, article title and article URL. It is a variant of the popular
[`cowsay`][cowsay] program.

  [cowsay]: https://en.wikipedia.org/wiki/Cowsay
  [Discover .NET]: https://discoverdot.net/
  [dotnet-cowsay]: https://github.com/isaacrlevin/dotnet-cowsay/tree/d94facb0721efad77c50864d1ad6112f694345b5

Back in the Docker container shel prompt, download the LINQPad query file to
disk:

    wget -q -O cowsay.linq http://share.linqpad.net/9bfa4d.linq

and then run it:

    lpless cowsay.linq

You should then see an output like this:

    ⠏ Looking for an awesome article..
     _________________________________________________________
    / Evgeny Zborovsky                                        \
    | Using Native Facebook Login Button in Xamarin.Forms QA  |
    \ https://evgenyzborovsky.com/2019/09/10/facebook-sdk-qa/ /
    ---------------------------------------------------------    
    
         \  ^__^
          \ (oo)\_______
            (__)\       )\/\
                ||----w |
                ||     ||


  [repo]: https://github.com/atifaziz/LinqPadless
  [Docker]: https://www.docker.com/
  [Alpine Linux]: https://alpinelinux.org/
