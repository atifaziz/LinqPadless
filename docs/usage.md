# Usage

Compile and run a single LINQPad query file in the current directory:

    lpless Foobar.linq

Compile but don't run:

    lpless -x Foobar.linq

Force a re-compilation before running even if the LINQPad query file has not
changed since the last run:

    lpless -f Foobar.linq

For more information, see help:

    lpless -h
