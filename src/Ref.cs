#region Copyright (c) 2014 Atif Aziz. All rights reserved.
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//
#endregion

using System;
using System.Diagnostics;

static partial class Ref
{
    public static Ref<T> Create<T>(T value) => new Ref<T>(value);
}

[DebuggerDisplay("{" + nameof(Value) + "}")]
sealed partial class Ref<T> : IFormattable
{
    public T Value { get; set; }
    public Ref(T value) => Value = value;
    public override string ToString() => $"{Value}";

    static readonly bool IsFormattable = typeof(IFormattable).IsAssignableFrom(typeof(T));

    public string ToString(string format, IFormatProvider formatProvider)
        => IsFormattable
         ? ((IFormattable) Value).ToString(format, formatProvider)
         : ToString();

    public static implicit operator T(Ref<T> reference)
        => reference == null ? default : reference.Value;
}
