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

namespace Mannex.IO
{
    #region Imports

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    #endregion

    /// <summary>
    /// Extension methods for <see cref="DirectoryInfo"/>.
    /// </summary>

    static partial class DirectoryInfoExtensions
    {
        /// <summary>
        /// Returns all the parents of the directory.
        /// </summary>
        /// <remarks>
        /// This method uses deferred execution. In addition, it does not
        /// check for the existence of the directory or its parents.
        /// </remarks>

        public static IEnumerable<DirectoryInfo> Parents(this DirectoryInfo dir)
        {
            return dir.SelfAndParents().Skip(1);
        }

        /// <summary>
        /// Returns the directory and all its parents.
        /// </summary>
        /// <remarks>
        /// This method uses deferred execution. In addition, it does not
        /// check for the existence of the directory or its parents.
        /// </remarks>

        public static IEnumerable<DirectoryInfo> SelfAndParents(this DirectoryInfo dir)
        {
            if (dir == null) throw new ArgumentNullException(nameof(dir));

            return _(); IEnumerable<DirectoryInfo> _()
            {
                for (; dir != null; dir = dir.Parent)
                    yield return dir;
            }
        }
    }
}
