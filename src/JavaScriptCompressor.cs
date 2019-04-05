#region Byline & Disclaimer
//
//  Author(s):
//
//      Atif Aziz (http://www.raboof.com)
//
//      Portion Copyright (c) 2001 Douglas Crockford
//      http://www.crockford.com
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
#endregion

namespace Jazmin
{
    #region Imports

    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.Serialization;

    #endregion

    #region About JavaScriptCompressor (JSMin)

    /*

    JavaScriptCompressor is a C# port of jsmin.c that was originally written
    by Douglas Crockford.

        jsmin.c
        04-Dec-2003
        (c) 2001 Douglas Crockford
        http://www.crockford.com

        C# port written by Atif Aziz
        12-Apr-2005
        http://www.raboof.com

    The following documentation is a minimal adaption from the original
    found at:

        http://www.crockford.com/javascript/jsmin.html

    The documentation therefore still refers to JSMin, but equally
    applies to this C# port since the code implementation has not been
    changed or enhanced in any way. Some passages have been omitted
    since they don't apply. For example, the original documentation has
    a comment about character set. This does not apply to this port
    since JavaScriptCompressor works with TextReader and TextWriter
    from the Base Class Library (BCL). The character set responsibility
    is therefore pushed back to the user of this class.



    What JSMin Does
    ---------------

    JSMin is a filter that omits or modifies some characters. This does
    not change the behavior of the program that it is minifying. The
    result may be harder to debug. It will definitely be harder to read.

    JSMin first replaces carriage returns ('\r') with linefeeds ('\n').
    It replaces all other control characters (including tab) with spaces.
    It replaces comments in the // form with linefeeds. It replaces
    comments with spaces. All runs of spaces are replaced with a single
    space. All runs of linefeeds are replaced with a single linefeed.

    It omits spaces except when a space is preceded or followed by a
    non-ASCII character or by an ASCII letter or digit, or by one of
    these characters:

    \ $ _

    It is more conservative in omitting linefeeds, because linefeeds are
    sometimes treated as semicolons. A linefeed is not omitted if it
    precedes a non-ASCII character or an ASCII letter or digit or one of
    these characters:

    \ $ _ { [ ( + -

    and if it follows a non-ASCII character or an ASCII letter or digit
    or one of these characters:

    \ $ _ } ] ) + - " '

    No other characters are omitted or modified.

    JSMin knows to not modify quoted strings and regular expression
    literals.

    JSMin does not obfuscate, but it does uglify.

    Before:

        // is.js

        // (c) 2001 Douglas Crockford
        // 2001 June 3


        // is

        // The -is- object is used to identify the browser.  Every browser edition
        // identifies itself, but there is no standard way of doing it, and some of
        // the identification is deceptive. This is because the authors of web
        // browsers are liars. For example, Microsoft's IE browsers claim to be
        // Mozilla 4. Netscape 6 claims to be version 5.

        var is = {
            ie:      navigator.appName == 'Microsoft Internet Explorer',
            java:    navigator.javaEnabled(),
            ns:      navigator.appName == 'Netscape',
            ua:      navigator.userAgent.toLowerCase(),
            version: parseFloat(navigator.appVersion.substr(21)) ||
                    parseFloat(navigator.appVersion),
            win:     navigator.platform == 'Win32'
        }
        is.mac = is.ua.indexOf('mac') >= 0;
        if (is.ua.indexOf('opera') >= 0) {
            is.ie = is.ns = false;
            is.opera = true;
        }
        if (is.ua.indexOf('gecko') >= 0) {
            is.ie = is.ns = false;
            is.gecko = true;
        }

    After:

        var is={ie:navigator.appName=='MicrosoftInternetExplorer',java:navigator.javaEnabled(),ns:navigator.appName=='Netscape',ua:navigator.userAgent.toLowerCase(),version:parseFloat(navigator.appVersion.substr(21))||parseFloat(navigator.appVersion),win:navigator.platform=='Win32'}
        is.mac=is.ua.indexOf('mac')>=0;if(is.ua.indexOf('opera')>=0){is.ie=is.ns=false;is.opera=true;}
        if(is.ua.indexOf('gecko')>=0){is.ie=is.ns=false;is.gecko=true;}



    Caution
    -------

    Do not put raw control characters inside a quoted string. That is an
    extremely bad practice. Use \xhh notation instead. JSMin will replace
    control characters with spaces or linefeeds.

    Use parens with confusing sequences of + or -. For example, minification
    changes

        a + ++b

    into

        a+++b

    which is interpreted as

        a++ + b

    which is wrong. You can avoid this by using parens:

        a + (++b)

    JSLint (http://www.jslint.com/) checks for all of these problems. It is
    suggested that JSLint be used before using JSMin.



    Errors
    ------

    JSMin can detect and produce three error messages:

        - Unterminated comment.
        - Unterminated string constant.
        - Unterminated regular expression.

    It ignores all other errors that may be present in your source program.

    */

    #endregion

    partial class JavaScriptCompressor
    {
        //
        // Public functions
        //

        public static string Compress(string source)
        {
            var writer = new StringWriter();
            Compress(new StringReader(source), writer);
            return writer.ToString();
        }

        public static void Compress(TextReader reader, TextWriter writer)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            var compressor = new JavaScriptCompressor(reader, writer);
            compressor.Compress();
        }

        //
        // Private implementation
        //

        int aa;
        int bb;
        int lookahead = eof;
        readonly TextReader reader;
        readonly TextWriter writer;

        const int eof = -1;

        JavaScriptCompressor(TextReader reader, TextWriter writer)
        {
            Debug.Assert(reader != null);
            Debug.Assert(writer != null);

            this.reader = reader;
            this.writer = writer;
        }

        /* Compress -- Copy the input to the output, deleting the characters which are
                insignificant to JavaScript. Comments will be removed. Tabs will be
                replaced with spaces. Carriage returns will be replaced with linefeeds.
                Most spaces and linefeeds will be removed.
        */

        void Compress()
        {
            aa = '\n';
            Action(3);
            while (aa != eof)
            {
                switch (aa)
                {
                    case ' ':
                        Action(IsAlphanum(bb) ? 1 : 2);
                        break;
                    case '\n':
                    switch (bb)
                    {
                        case '{':
                        case '[':
                        case '(':
                        case '+':
                        case '-':
                            Action(1);
                            break;
                        case ' ':
                            Action(3);
                            break;
                        default:
                            Action(IsAlphanum(bb) ? 1 : 2);
                            break;
                    }
                        break;
                    default:
                    switch (bb)
                    {
                        case ' ':
                            if (IsAlphanum(aa))
                            {
                                Action(1);
                                break;
                            }
                            Action(3);
                            break;
                        case '\n':
                        switch (aa)
                        {
                            case '}':
                            case ']':
                            case ')':
                            case '+':
                            case '-':
                            case '"':
                            case '\'':
                                Action(1);
                                break;
                            default:
                                Action(IsAlphanum(aa) ? 1 : 3);
                                break;
                        }
                            break;
                        default:
                            Action(1);
                            break;
                    }
                        break;
                }
            }
        }

        /* Get -- return the next character from stdin. Watch out for lookahead. If
                the character is a control character, translate it to a space or
                linefeed.
        */

        int Get()
        {
            var ch = lookahead;
            lookahead = eof;
            if (ch == eof)
            {
                ch = reader.Read();
            }
            if (ch >= ' ' || ch == '\n' || ch == eof)
            {
                return ch;
            }
            if (ch == '\r')
            {
                return '\n';
            }
            return ' ';
        }


        /* Peek -- get the next character without getting it.
        */

        int Peek() => lookahead = Get();

        /* Next -- get the next character, excluding comments. Peek() is used to see
                if a '/' is followed by a '/' or '*'.
        */

        int Next()
        {
            var ch = Get();
            if (ch == '/')
            {
                switch (Peek())
                {
                    case '/':
                        for (;; )
                        {
                            ch = Get();
                            if (ch <= '\n')
                            {
                                return ch;
                            }
                        }
                    case '*':
                        Get();
                        for (;; )
                        {
                            switch (Get())
                            {
                                case '*':
                                    if (Peek() == '/')
                                    {
                                        Get();
                                        return ' ';
                                    }
                                    break;
                                case eof:
                                    throw new Exception("Unterminated comment.");
                            }
                        }
                    default:
                        return ch;
                }
            }
            return ch;
        }


        /* Action -- do something! What you do is determined by the argument:
                1   Output A. Copy A to B. Get the next B.
                2   Copy B to A. Get the next B. (Delete A).
                3   Get the next B. (Delete B).
           Action treats a string as a single character. Wow!
           Action recognizes a regular expression if it is preceded by ( or , or =.
        */

        void Action(int d)
        {
            switch (d)
            {
                case 1:
                    Write(aa);
                    goto case 2;
                case 2:
                    aa = bb;
                    if (aa == '\'' || aa == '"')
                    {
                        for (;; )
                        {
                            Write(aa);
                            aa = Get();
                            if (aa == bb)
                            {
                                break;
                            }
                            if (aa <= '\n')
                            {
                                var message = $"Unterminated string literal: '{aa}'.";
                                throw new Exception(message);
                            }
                            if (aa == '\\')
                            {
                                Write(aa);
                                aa = Get();
                            }
                        }
                    }
                    goto case 3;
                case 3:
                    bb = Next();
                    if (bb == '/' && (aa == '(' || aa == ',' || aa == '='))
                    {
                        Write(aa);
                        Write(bb);
                        for (;; )
                        {
                            aa = Get();
                            if (aa == '/')
                            {
                                break;
                            }
                            else if (aa == '\\')
                            {
                                Write(aa);
                                aa = Get();
                            }
                            else if (aa <= '\n')
                            {
                                throw new Exception("Unterminated Regular Expression literal.");
                            }
                            Write(aa);
                        }
                        bb = Next();
                    }
                    break;
            }
        }

        void Write(int ch) => writer.Write((char) ch);

        /* IsAlphanum -- return true if the character is a letter, digit, underscore,
                dollar sign, or non-ASCII character.
        */

        static bool IsAlphanum(int ch)
            => ch >= 'a' && ch <= 'z'
            || ch >= '0' && ch <= '9'
            || ch >= 'A' && ch <= 'Z'
            || ch == '_' || ch == '$' || ch == '\\' || ch > 126;

        [Serializable]
        public class Exception : System.Exception
        {
            public Exception() {}

            public Exception(string message) :
                base(message) {}

            public Exception(string message, System.Exception innerException) :
                base(message, innerException) {}

            protected Exception(SerializationInfo info, StreamingContext context) :
                base(info, context) {}
        }
    }
}
