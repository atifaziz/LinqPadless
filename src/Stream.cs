#region Copyright (c) 2018 Atif Aziz. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

namespace LinqPadless
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    interface IStreamable
    {
        Stream Open();
    }

    static class Streamable
    {
        public static IStreamable Create(Func<Stream> opener) =>
            new DelegatingStreamable(opener);

        public static IStreamable ReadFile(string path) =>
            Create(() => File.OpenRead(path));

        sealed class DelegatingStreamable(Func<Stream> opener) : IStreamable
        {
            readonly Func<Stream> opener = opener ?? throw new ArgumentNullException(nameof(opener));

            public Stream Open() => this.opener();
        }
    }

    static class StreamExtensions
    {
        public static IStreamable MapText(this IStreamable streamable,
                                          Func<string, string> mapper) =>
            MapText(streamable, Utf8.BomlessEncoding, mapper);

        public static IStreamable MapText(this IStreamable streamable,
                                          Encoding outputEncoding,
                                          Func<string, string> mapper)
        {
            if (streamable == null) throw new ArgumentNullException(nameof(streamable));
            if (outputEncoding == null) throw new ArgumentNullException(nameof(outputEncoding));
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));

            return Streamable.Create(() =>
            {
                var mapped = mapper(streamable.ReadText());
                return new MemoryStream(outputEncoding.GetBytes(mapped));
            });
        }

        public static IStreamable ToStreamable(this IEnumerable<Stream> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return Streamable.Create(() => new CompositeStream(source.GetEnumerator()));
        }

        sealed class CompositeStream(IEnumerator<Stream> source) : Stream
        {
            IEnumerator<Stream> streams = source ?? throw new ArgumentNullException(nameof(source));
            Stream stream;

            public override void Flush() => throw new NotSupportedException();

            public override void Close()
            {
                if (Assignment.Reset(ref this.streams) is { } streams)
                    streams.Dispose();

                base.Close();
            }

            Stream GetSubStream()
            {
                var stream = this.stream;

                if (stream != null)
                    return stream;

                var streams = this.streams;
                if (streams == null)
                    return null;

                if (!streams.MoveNext())
                {
                    streams.Dispose();
                    this.streams = null;
                    return null;
                }

                return this.stream = streams.Current;
            }

            void OnEndOfStream()
            {
                var stream = Assignment.Reset(ref this.stream);
                Debug.Assert(stream != null);
                stream.Close();
            }

            public override int ReadByte()
            {
                for (var stream = GetSubStream(); stream != null; stream = GetSubStream())
                {
                    var b = stream.ReadByte();
                    if (b >= 0)
                        return b;
                    OnEndOfStream();
                }

                return -1;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                for (var stream = GetSubStream(); stream != null; stream = GetSubStream())
                {
                    var read = stream.Read(buffer, offset, count);
                    if (read > 0)
                        return read;
                    OnEndOfStream();
                }

                return 0;
            }

            public override long Seek(long offset, SeekOrigin origin) =>
                throw new NotSupportedException();

            public override void SetLength(long value) =>
                throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }
        }

        public static string ReadText(this IStreamable source) =>
            ReadText(source, null);

        public static string ReadText(this IStreamable source, Encoding encoding)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            using var stream = source.Open();
            using var reader = encoding != null ? new StreamReader(stream, encoding)
                                                : new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
