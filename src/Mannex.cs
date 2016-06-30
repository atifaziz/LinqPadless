#region License, Terms and Author(s)
//
// Mannex - Extension methods for .NET
// Copyright (c) 2009 Atif Aziz. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

namespace Mannex
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Extension methods for <see cref="string"/>.
    /// </summary>

    static partial class StringExtensions
    {
        /// <summary>
        /// Splits a string into a pair using a specified character to
        /// separate the two.
        /// </summary>
        /// <remarks>
        /// Neither half in the resulting pair is ever <c>null</c>.
        /// </remarks>

        public static T Split<T>(this string str, char separator, Func<string, string, T> resultFunc)
        {
            if (str == null) throw new ArgumentNullException("str");
            if (resultFunc == null) throw new ArgumentNullException("resultFunc");
            return SplitRemoving(str, str.IndexOf(separator), 1, resultFunc);
        }

        /// <summary>
        /// Splits a string into a pair by removing a portion of the string.
        /// </summary>
        /// <remarks>
        /// Neither half in the resulting pair is ever <c>null</c>.
        /// </remarks>

        static T SplitRemoving<T>(string str, int index, int count, Func<string, string, T> resultFunc)
        {
            Debug.Assert(str != null);
            Debug.Assert(count > 0);
            Debug.Assert(resultFunc != null);

            var a = index < 0
                  ? str
                  : str.Substring(0, index);

            var b = index < 0 || index + 1 >= str.Length
                  ? string.Empty
                  : str.Substring(index + count);

            return resultFunc(a, b);
        }
    }
}

namespace Mannex.IO
{
    #region Imports

    using System;
    using System.Collections.Generic;
    using System.IO;

    #endregion

    /// <summary>
    /// Extension methods for <see cref="TextReader"/>.
    /// </summary>

    static partial class TextReaderExtensions
    {
        /// <summary>
        /// Reads all lines from reader using deferred semantics.
        /// </summary>

        public static IEnumerator<string> ReadLines(this TextReader reader)
        {
            if (reader == null) throw new ArgumentNullException("reader");
            return ReadLinesImpl(reader);
        }

        static IEnumerator<string> ReadLinesImpl(this TextReader reader)
        {
            for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
                yield return line;
        }
    }
}

namespace Mannex.Collections.Generic
{
    using System.Collections.Generic;

    /// <summary>
    /// Extension methods for pairing keys and values as 
    /// <see cref="KeyValuePair{TKey,TValue}"/>.
    /// </summary>

    static partial class PairingExtensions
    {
        /// <summary>
        /// Pairs a value with a key.
        /// </summary>

        public static KeyValuePair<TKey, TValue> AsKeyTo<TKey, TValue>(this TKey key, TValue value)
        {
            return new KeyValuePair<TKey, TValue>(key, value);
        }
    }
}
