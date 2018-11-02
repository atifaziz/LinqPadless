#region Copyright (c) 2015 Atif Aziz. All rights reserved.
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

// ReSharper disable PartialTypeWithSinglePart

namespace Choices
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using static Choice.New;

    static partial class Choice
    {
        public static partial class New
        {
            public static Choice<T1, T2> Choice1<T1, T2>(T1 value) => Choice<T1, T2>.Choice1(value);
            public static Choice<T1, T2> Choice2<T1, T2>(T2 value) => Choice<T1, T2>.Choice2(value);

            public static Choice<T1, T2, T3> Choice1<T1, T2, T3>(T1 value) => Choice<T1, T2, T3>.Choice1(value);
            public static Choice<T1, T2, T3> Choice2<T1, T2, T3>(T2 value) => Choice<T1, T2, T3>.Choice2(value);
            public static Choice<T1, T2, T3> Choice3<T1, T2, T3>(T3 value) => Choice<T1, T2, T3>.Choice3(value);

            public static Choice<T1, T2, T3, T4> Choice1<T1, T2, T3, T4>(T1 value) => Choice<T1, T2, T3, T4>.Choice1(value);
            public static Choice<T1, T2, T3, T4> Choice2<T1, T2, T3, T4>(T2 value) => Choice<T1, T2, T3, T4>.Choice2(value);
            public static Choice<T1, T2, T3, T4> Choice3<T1, T2, T3, T4>(T3 value) => Choice<T1, T2, T3, T4>.Choice3(value);
            public static Choice<T1, T2, T3, T4> Choice4<T1, T2, T3, T4>(T4 value) => Choice<T1, T2, T3, T4>.Choice4(value);

            public static Choice<T1, T2, T3, T4, T5> Choice1<T1, T2, T3, T4, T5>(T1 value) => Choice<T1, T2, T3, T4, T5>.Choice1(value);
            public static Choice<T1, T2, T3, T4, T5> Choice2<T1, T2, T3, T4, T5>(T2 value) => Choice<T1, T2, T3, T4, T5>.Choice2(value);
            public static Choice<T1, T2, T3, T4, T5> Choice3<T1, T2, T3, T4, T5>(T3 value) => Choice<T1, T2, T3, T4, T5>.Choice3(value);
            public static Choice<T1, T2, T3, T4, T5> Choice4<T1, T2, T3, T4, T5>(T4 value) => Choice<T1, T2, T3, T4, T5>.Choice4(value);
            public static Choice<T1, T2, T3, T4, T5> Choice5<T1, T2, T3, T4, T5>(T5 value) => Choice<T1, T2, T3, T4, T5>.Choice5(value);

            /* TODO Choice<T1, T2, T3, T4, T5, T6>
            public static Choice<T1, T2, T3, T4, T5, T6> Choice1<T1, T2, T3, T4, T5, T6>(T1 value) => Choice<T1, T2, T3, T4, T5, T6>.Choice1(value);
            public static Choice<T1, T2, T3, T4, T5, T6> Choice2<T1, T2, T3, T4, T5, T6>(T2 value) => Choice<T1, T2, T3, T4, T5, T6>.Choice2(value);
            public static Choice<T1, T2, T3, T4, T5, T6> Choice3<T1, T2, T3, T4, T5, T6>(T3 value) => Choice<T1, T2, T3, T4, T5, T6>.Choice3(value);
            public static Choice<T1, T2, T3, T4, T5, T6> Choice4<T1, T2, T3, T4, T5, T6>(T4 value) => Choice<T1, T2, T3, T4, T5, T6>.Choice4(value);
            public static Choice<T1, T2, T3, T4, T5, T6> Choice5<T1, T2, T3, T4, T5, T6>(T5 value) => Choice<T1, T2, T3, T4, T5, T6>.Choice5(value);
            public static Choice<T1, T2, T3, T4, T5, T6> Choice6<T1, T2, T3, T4, T5, T6>(T6 value) => Choice<T1, T2, T3, T4, T5, T6>.Choice6(value);
            */
            /* TODO Choice<T1, T2, T3, T4, T5, T6, T7>
            public static Choice<T1, T2, T3, T4, T5, T6, T7> Choice1<T1, T2, T3, T4, T5, T6, T7>(T1 value) => Choice<T1, T2, T3, T4, T5, T6, T7>.Choice1(value);
            public static Choice<T1, T2, T3, T4, T5, T6, T7> Choice2<T1, T2, T3, T4, T5, T6, T7>(T2 value) => Choice<T1, T2, T3, T4, T5, T6, T7>.Choice2(value);
            public static Choice<T1, T2, T3, T4, T5, T6, T7> Choice3<T1, T2, T3, T4, T5, T6, T7>(T3 value) => Choice<T1, T2, T3, T4, T5, T6, T7>.Choice3(value);
            public static Choice<T1, T2, T3, T4, T5, T6, T7> Choice4<T1, T2, T3, T4, T5, T6, T7>(T4 value) => Choice<T1, T2, T3, T4, T5, T6, T7>.Choice4(value);
            public static Choice<T1, T2, T3, T4, T5, T6, T7> Choice5<T1, T2, T3, T4, T5, T6, T7>(T5 value) => Choice<T1, T2, T3, T4, T5, T6, T7>.Choice5(value);
            public static Choice<T1, T2, T3, T4, T5, T6, T7> Choice6<T1, T2, T3, T4, T5, T6, T7>(T6 value) => Choice<T1, T2, T3, T4, T5, T6, T7>.Choice6(value);
            public static Choice<T1, T2, T3, T4, T5, T6, T7> Choice7<T1, T2, T3, T4, T5, T6, T7>(T7 value) => Choice<T1, T2, T3, T4, T5, T6, T7>.Choice7(value);
            */
            /* TODO Choice<T1, T2, T3, T4, T5, T6, T7, T8>
            public static Choice<T1, T2, T3, T4, T5, T6, T7, T8> Choice1<T1, T2, T3, T4, T5, T6, T7, T8>(T1 value) => Choice<T1, T2, T3, T4, T5, T6, T7, T8>.Choice1(value);
            public static Choice<T1, T2, T3, T4, T5, T6, T7, T8> Choice2<T1, T2, T3, T4, T5, T6, T7, T8>(T2 value) => Choice<T1, T2, T3, T4, T5, T6, T7, T8>.Choice2(value);
            public static Choice<T1, T2, T3, T4, T5, T6, T7, T8> Choice3<T1, T2, T3, T4, T5, T6, T7, T8>(T3 value) => Choice<T1, T2, T3, T4, T5, T6, T7, T8>.Choice3(value);
            public static Choice<T1, T2, T3, T4, T5, T6, T7, T8> Choice4<T1, T2, T3, T4, T5, T6, T7, T8>(T4 value) => Choice<T1, T2, T3, T4, T5, T6, T7, T8>.Choice4(value);
            public static Choice<T1, T2, T3, T4, T5, T6, T7, T8> Choice5<T1, T2, T3, T4, T5, T6, T7, T8>(T5 value) => Choice<T1, T2, T3, T4, T5, T6, T7, T8>.Choice5(value);
            public static Choice<T1, T2, T3, T4, T5, T6, T7, T8> Choice6<T1, T2, T3, T4, T5, T6, T7, T8>(T6 value) => Choice<T1, T2, T3, T4, T5, T6, T7, T8>.Choice6(value);
            public static Choice<T1, T2, T3, T4, T5, T6, T7, T8> Choice7<T1, T2, T3, T4, T5, T6, T7, T8>(T7 value) => Choice<T1, T2, T3, T4, T5, T6, T7, T8>.Choice7(value);
            public static Choice<T1, T2, T3, T4, T5, T6, T7, T8> Choice8<T1, T2, T3, T4, T5, T6, T7, T8>(T8 value) => Choice<T1, T2, T3, T4, T5, T6, T7, T8>.Choice8(value);
            */
        }

        public static Choice<T1, T2> If<T1, T2>(bool flag, Func<T1> t, Func<T2> f)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (f == null) throw new ArgumentNullException(nameof(f));

            return flag ? Choice1<T1, T2>(t())
                        : Choice2<T1, T2>(f());
        }

        public static Choice<T1, T2, T3> If<T1, T2, T3>(bool flag, Func<T1> t, Func<Choice<T2, T3>> f)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (f == null) throw new ArgumentNullException(nameof(f));

            return flag
                 ? Choice1<T1, T2, T3>(t())
                 : f().Match(Choice2<T1, T2, T3>,
                             Choice3<T1, T2, T3>);
        }

        public static Choice<T1, T2, T3, T4> If<T1, T2, T3, T4>(bool flag, Func<T1> t, Func<Choice<T2, T3, T4>> f)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (f == null) throw new ArgumentNullException(nameof(f));

            return flag
                 ? Choice1<T1, T2, T3, T4>(t())
                 : f().Match(Choice2<T1, T2, T3, T4>,
                             Choice3<T1, T2, T3, T4>,
                             Choice4<T1, T2, T3, T4>);
        }

        public static Choice<T1, T2, T3, T4, T5> If<T1, T2, T3, T4, T5>(bool flag, Func<T1> t, Func<Choice<T2, T3, T4, T5>> f)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (f == null) throw new ArgumentNullException(nameof(f));

            return flag
                ? Choice1<T1, T2, T3, T4, T5>(t())
                : f().Match(Choice2<T1, T2, T3, T4, T5>,
                            Choice3<T1, T2, T3, T4, T5>,
                            Choice4<T1, T2, T3, T4, T5>,
                            Choice5<T1, T2, T3, T4, T5>);
        }

        public static Choice<T2, T3> Forbid1<T1, T2, T3>(this Choice<T1, T2, T3> choice) =>
            choice.Match(_ => throw new InvalidOperationException(),
                         Choice1<T2, T3>,
                         Choice2<T2, T3>);

        public static Choice<T1, T3> Forbid2<T1, T2, T3>(this Choice<T1, T2, T3> choice) =>
            choice.Match(Choice1<T1, T3>,
                         _ => throw new InvalidOperationException(),
                         Choice2<T1, T3>);

        public static Choice<T1, T2> Forbid3<T1, T2, T3>(this Choice<T1, T2, T3> choice) =>
            choice.Match(Choice1<T1, T2>,
                         Choice2<T1, T2>,
                         _ => throw new InvalidOperationException());

        public static Choice<T2, T3, T4> Forbid1<T1, T2, T3, T4>(this Choice<T1, T2, T3, T4> choice) =>
            choice.Match(_ => throw new InvalidOperationException(),
                         Choice1<T2, T3, T4>,
                         Choice2<T2, T3, T4>,
                         Choice3<T2, T3, T4>);

        public static Choice<T1, T3, T4> Forbid2<T1, T2, T3, T4>(this Choice<T1, T2, T3, T4> choice) =>
            choice.Match(Choice1<T1, T3, T4>,
                         _ => throw new InvalidOperationException(),
                         Choice2<T1, T3, T4>,
                         Choice3<T1, T3, T4>);

        public static Choice<T1, T2, T4> Forbid3<T1, T2, T3, T4>(this Choice<T1, T2, T3, T4> choice) =>
            choice.Match(Choice1<T1, T2, T4>,
                         Choice2<T1, T2, T4>,
                         _ => throw new InvalidOperationException(),
                         Choice3<T1, T2, T4>);

        public static Choice<T1, T2, T3> Forbid4<T1, T2, T3, T4>(this Choice<T1, T2, T3, T4> choice) =>
            choice.Match(Choice1<T1, T2, T3>,
                         Choice2<T1, T2, T3>,
                         Choice3<T1, T2, T3>,
                         _ => throw new InvalidOperationException());

        public static Choice<T2, T3, T4, T5> Forbid1<T1, T2, T3, T4, T5>(this Choice<T1, T2, T3, T4, T5> choice) =>
            choice.Match(_ => throw new InvalidOperationException(),
                         Choice1<T2, T3, T4, T5>,
                         Choice2<T2, T3, T4, T5>,
                         Choice3<T2, T3, T4, T5>,
                         Choice4<T2, T3, T4, T5>);

        public static Choice<T1, T3, T4, T5> Forbid2<T1, T2, T3, T4, T5>(this Choice<T1, T2, T3, T4, T5> choice) =>
            choice.Match(Choice1<T1, T3, T4, T5>,
                         _ => throw new InvalidOperationException(),
                         Choice2<T1, T3, T4, T5>,
                         Choice3<T1, T3, T4, T5>,
                         Choice4<T1, T3, T4, T5>);

        public static Choice<T1, T2, T4, T5> Forbid3<T1, T2, T3, T4, T5>(this Choice<T1, T2, T3, T4, T5> choice) =>
            choice.Match(Choice1<T1, T2, T4, T5>,
                         Choice2<T1, T2, T4, T5>,
                         _ => throw new InvalidOperationException(),
                         Choice3<T1, T2, T4, T5>,
                         Choice4<T1, T2, T4, T5>);

        public static Choice<T1, T2, T3, T5> Forbid4<T1, T2, T3, T4, T5>(this Choice<T1, T2, T3, T4, T5> choice) =>
            choice.Match(Choice1<T1, T2, T3, T5>,
                         Choice2<T1, T2, T3, T5>,
                         Choice3<T1, T2, T3, T5>,
                         _ => throw new InvalidOperationException(),
                         Choice4<T1, T2, T3, T5>);

        public static Choice<T1, T2, T3, T4> Forbid5<T1, T2, T3, T4, T5>(this Choice<T1, T2, T3, T4, T5> choice) =>
            choice.Match(Choice1<T1, T2, T3, T4>,
                         Choice2<T1, T2, T3, T4>,
                         Choice3<T1, T2, T3, T4>,
                         Choice4<T1, T2, T3, T4>,
                         _ => throw new InvalidOperationException());

        public static WhenPartial<T, TResult> When1<T, TResult>(Func<T, TResult> selector)
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return new WhenPartial<T, TResult>(selector);
        }

        public static Func<Choice<T1, T2>, TResult> When2<T1, T2, TResult>(this WhenPartial<T1, TResult> otherwise, Func<T2, TResult> selector)
        {
            if (otherwise == null) throw new ArgumentNullException(nameof(otherwise));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            return choice => choice.Match(otherwise.First, selector);
        }

        public static Func<Choice<T1, T2, T3>, TResult> When3<T1, T2, T3, TResult>(this Func<Choice<T1, T2>, TResult> otherwise, Func<T3, TResult> selector)
        {
            if (otherwise == null) throw new ArgumentNullException(nameof(otherwise));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            return choice =>
                choice.Match(first  => otherwise(Choice1<T1, T2>(first)),
                             second => otherwise(Choice2<T1, T2>(second)),
                             selector);
        }

        public static Func<Choice<T1, T2, T3, T4>, TResult> When4<T1, T2, T3, T4, TResult>(this Func<Choice<T1, T2, T3>, TResult> otherwise, Func<T4, TResult> selector)
        {
            if (otherwise == null) throw new ArgumentNullException(nameof(otherwise));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            return choice =>
                choice.Match(first  => otherwise(Choice1<T1, T2, T3>(first)),
                             second => otherwise(Choice2<T1, T2, T3>(second)),
                             third  => otherwise(Choice3<T1, T2, T3>(third)),
                             selector);
        }

        public static Func<Choice<T1, T2, T3, T4, T5>, TResult> When5<T1, T2, T3, T4, T5, TResult>(this Func<Choice<T1, T2, T3, T4>, TResult> otherwise, Func<T5, TResult> selector)
        {
            if (otherwise == null) throw new ArgumentNullException(nameof(otherwise));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            return choice =>
                choice.Match(first  => otherwise(Choice1<T1, T2, T3, T4>(first)),
                             second => otherwise(Choice2<T1, T2, T3, T4>(second)),
                             third  => otherwise(Choice3<T1, T2, T3, T4>(third)),
                             fourth => otherwise(Choice4<T1, T2, T3, T4>(fourth)),
                             selector);
        }

        public static Choice<TResult1, TResult2>
            Map<T1, T2, TResult1, TResult2>(
                this Choice<T1, T2> choice,
                Func<T1, TResult1> selector1,
                Func<T2, TResult2> selector2) =>
            choice.Match(
                a => Choice<TResult1, TResult2>.Choice1(selector1(a)),
                b => Choice<TResult1, TResult2>.Choice2(selector2(b)));

        public static Choice<TResult1, TResult2, TResult3>
            Map<T1, T2, T3, TResult1, TResult2, TResult3>(
                this Choice<T1, T2, T3> choice,
                Func<T1, TResult1> selector1,
                Func<T2, TResult2> selector2,
                Func<T3, TResult3> selector3) =>
            choice.Match(
                a => Choice<TResult1, TResult2, TResult3>.Choice1(selector1(a)),
                b => Choice<TResult1, TResult2, TResult3>.Choice2(selector2(b)),
                c => Choice<TResult1, TResult2, TResult3>.Choice3(selector3(c)));

        public static Choice<TResult1, TResult2, TResult3, TResult4>
            Map<T1, T2, T3, T4, TResult1, TResult2, TResult3, TResult4>(
                this Choice<T1, T2, T3, T4> choice,
                Func<T1, TResult1> selector1,
                Func<T2, TResult2> selector2,
                Func<T3, TResult3> selector3,
                Func<T4, TResult4> selector4) =>
            choice.Match(
                a => Choice<TResult1, TResult2, TResult3, TResult4>.Choice1(selector1(a)),
                b => Choice<TResult1, TResult2, TResult3, TResult4>.Choice2(selector2(b)),
                c => Choice<TResult1, TResult2, TResult3, TResult4>.Choice3(selector3(c)),
                d => Choice<TResult1, TResult2, TResult3, TResult4>.Choice4(selector4(d)));

        public static Choice<TResult1, TResult2, TResult3, TResult4, TResult5>
            Map<T1, T2, T3, T4, T5, TResult1, TResult2, TResult3, TResult4, TResult5>(
                this Choice<T1, T2, T3, T4, T5> choice,
                Func<T1, TResult1> selector1,
                Func<T2, TResult2> selector2,
                Func<T3, TResult3> selector3,
                Func<T4, TResult4> selector4,
                Func<T5, TResult5> selector5) =>
            choice.Match(
                a => Choice<TResult1, TResult2, TResult3, TResult4, TResult5>.Choice1(selector1(a)),
                b => Choice<TResult1, TResult2, TResult3, TResult4, TResult5>.Choice2(selector2(b)),
                c => Choice<TResult1, TResult2, TResult3, TResult4, TResult5>.Choice3(selector3(c)),
                d => Choice<TResult1, TResult2, TResult3, TResult4, TResult5>.Choice4(selector4(d)),
                e => Choice<TResult1, TResult2, TResult3, TResult4, TResult5>.Choice5(selector5(e)));

        public static Choice<TResult, T2> Map1<T1, T2, TResult>(this Choice<T1, T2> choice, Func<T1, TResult> selector) =>
            choice.Map(selector, x => x);

        public static Choice<T1, TResult> Map2<T1, T2, TResult>(this Choice<T1, T2> choice, Func<T2, TResult> selector) =>
            choice.Map(x => x, selector);

        public static Choice<TResult, T2, T3> Map1<T1, T2, T3, TResult>(this Choice<T1, T2, T3> choice, Func<T1, TResult> selector) =>
            choice.Map(selector, x => x, x => x);

        public static Choice<T1, TResult, T3> Map2<T1, T2, T3, TResult>(this Choice<T1, T2, T3> choice, Func<T2, TResult> selector) =>
            choice.Map(x => x, selector, x => x);

        public static Choice<T1, T2, TResult> Map3<T1, T2, T3, TResult>(this Choice<T1, T2, T3> choice, Func<T3, TResult> selector) =>
            choice.Map(x => x, x => x, selector);

        public static Choice<TResult, T2, T3, T4> Map1<T1, T2, T3, T4, TResult>(this Choice<T1, T2, T3, T4> choice, Func<T1, TResult> selector) =>
            choice.Map(selector, x => x, x => x, x => x);

        public static Choice<T1, TResult, T3, T4> Map2<T1, T2, T3, T4, TResult>(this Choice<T1, T2, T3, T4> choice, Func<T2, TResult> selector) =>
            choice.Map(x => x, selector, x => x, x => x);

        public static Choice<T1, T2, TResult, T4> Map3<T1, T2, T3, T4, TResult>(this Choice<T1, T2, T3, T4> choice, Func<T3, TResult> selector) =>
            choice.Map(x => x, x => x, selector, x => x);

        public static Choice<T1, T2, T3, TResult> Map4<T1, T2, T3, T4, TResult>(this Choice<T1, T2, T3, T4> choice, Func<T4, TResult> selector) =>
            choice.Map(x => x, x => x, x => x, selector);

        public static Choice<TResult, T2, T3, T4, T5> Map1<T1, T2, T3, T4, T5, TResult>(this Choice<T1, T2, T3, T4, T5> choice, Func<T1, TResult> selector) =>
            choice.Map(selector, x => x, x => x, x => x, x => x);

        public static Choice<T1, TResult, T3, T4, T5> Map2<T1, T2, T3, T4, T5, TResult>(this Choice<T1, T2, T3, T4, T5> choice, Func<T2, TResult> selector) =>
            choice.Map(x => x, selector, x => x, x => x, x => x);

        public static Choice<T1, T2, TResult, T4, T5> Map3<T1, T2, T3, T4, T5, TResult>(this Choice<T1, T2, T3, T4, T5> choice, Func<T3, TResult> selector) =>
            choice.Map(x => x, x => x, selector, x => x, x => x);

        public static Choice<T1, T2, T3, TResult, T5> Map4<T1, T2, T3, T4, T5, TResult>(this Choice<T1, T2, T3, T4, T5> choice, Func<T4, TResult> selector) =>
            choice.Map(x => x, x => x, x => x, selector, x => x);

        public static Choice<T1, T2, T3, T4, TResult> Map5<T1, T2, T3, T4, T5, TResult>(this Choice<T1, T2, T3, T4, T5> choice, Func<T5, TResult> selector) =>
            choice.Map(x => x, x => x, x => x, x => x, selector);

        public static IReadOnlyList<Choice<T1, T2>>
            ToChoices<T1, T2>(this (T1, T2) tuple) => new[]
            {
                Choice1<T1, T2>(tuple.Item1),
                Choice2<T1, T2>(tuple.Item2),
            };

        public static IReadOnlyList<Choice<T1, T2, T3>>
            ToChoices<T1, T2, T3>(this (T1, T2, T3) tuple) => new[]
            {
                Choice1<T1, T2, T3>(tuple.Item1),
                Choice2<T1, T2, T3>(tuple.Item2),
                Choice3<T1, T2, T3>(tuple.Item3),
            };

        public static IReadOnlyList<Choice<T1, T2, T3, T4>>
            ToChoices<T1, T2, T3, T4>(this (T1, T2, T3, T4) tuple) => new[]
            {
                Choice1<T1, T2, T3, T4>(tuple.Item1),
                Choice2<T1, T2, T3, T4>(tuple.Item2),
                Choice3<T1, T2, T3, T4>(tuple.Item3),
                Choice4<T1, T2, T3, T4>(tuple.Item4),
            };

        public static IReadOnlyList<Choice<T1, T2, T3, T4, T5>>
            ToChoices<T1, T2, T3, T4, T5>(this (T1, T2, T3, T4, T5) tuple) => new[]
            {
                Choice1<T1, T2, T3, T4, T5>(tuple.Item1),
                Choice2<T1, T2, T3, T4, T5>(tuple.Item2),
                Choice3<T1, T2, T3, T4, T5>(tuple.Item3),
                Choice4<T1, T2, T3, T4, T5>(tuple.Item4),
                Choice5<T1, T2, T3, T4, T5>(tuple.Item5),
            };

        public static IReadOnlyList<Choice<T1, T2>>
            ToChoices<T1, T2>(this Tuple<T1, T2> tuple) => new[]
            {
                Choice1<T1, T2>(tuple.Item1),
                Choice2<T1, T2>(tuple.Item2),
            };

        public static IReadOnlyList<Choice<T1, T2, T3>>
            ToChoices<T1, T2, T3>(this Tuple<T1, T2, T3> tuple) => new[]
            {
                Choice1<T1, T2, T3>(tuple.Item1),
                Choice2<T1, T2, T3>(tuple.Item2),
                Choice3<T1, T2, T3>(tuple.Item3),
            };

        public static IReadOnlyList<Choice<T1, T2, T3, T4>>
            ToChoices<T1, T2, T3, T4>(this Tuple<T1, T2, T3, T4> tuple) => new[]
            {
                Choice1<T1, T2, T3, T4>(tuple.Item1),
                Choice2<T1, T2, T3, T4>(tuple.Item2),
                Choice3<T1, T2, T3, T4>(tuple.Item3),
                Choice4<T1, T2, T3, T4>(tuple.Item4),
            };

        public static IReadOnlyList<Choice<T1, T2, T3, T4, T5>>
            ToChoices<T1, T2, T3, T4, T5>(this Tuple<T1, T2, T3, T4, T5> tuple) => new[]
            {
                Choice1<T1, T2, T3, T4, T5>(tuple.Item1),
                Choice2<T1, T2, T3, T4, T5>(tuple.Item2),
                Choice3<T1, T2, T3, T4, T5>(tuple.Item3),
                Choice4<T1, T2, T3, T4, T5>(tuple.Item4),
                Choice5<T1, T2, T3, T4, T5>(tuple.Item5),
            };

        internal static bool Equals<T1, T2, TValue>(T1 @this, T2 that,
                                                    Func<T1, TValue> choiceSelector)
            => that != null
            && (ReferenceEquals(that, @this)
                || that is T1 c && EqualityComparer.Equals(choiceSelector(@this), choiceSelector(c)));

        public static int GetHashCode<T, TValue>(T choice, TValue value) =>
            unchecked((typeof(T).GetHashCode() * 397) ^ EqualityComparer.GetHashCode(value));

        internal static string ToString<T>(T value) =>
            value?.ToString() ?? string.Empty;
    }

    sealed partial class WhenPartial<T, TResult>
    {
        internal readonly Func<T, TResult> First;
        public WhenPartial(Func<T, TResult> first) =>
            First = first ?? throw new ArgumentNullException(nameof(first));
    }

    static class EqualityComparer
    {
        public static int GetHashCode<T>(T value) =>
            EqualityComparer<T>.Default.GetHashCode(value);

        public static bool Equals<T>(T x, T y) =>
            EqualityComparer<T>.Default.Equals(x, y);
    }

    abstract partial class Choice<T1, T2> : IEquatable<Choice<T1, T2>>
    {
        internal static Choice<T1, T2> Choice1(T1 value) => new Choice1Of2(value);
        internal static Choice<T1, T2> Choice2(T2 value) => new Choice2Of2(value);

        public abstract TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second);

        public abstract override int GetHashCode();
        public abstract override bool Equals(object obj);
        public abstract bool Equals(Choice<T1, T2> other);

        public abstract override string ToString();

        [DebuggerDisplay(nameof(Choice1Of2) + ": {" + nameof(_value) + "}")]
        sealed partial class Choice1Of2 : Choice<T1, T2>
        {
            readonly T1 _value;

            public Choice1Of2(T1 value) => _value = value;

            public override TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second)
            {
                if (first == null) throw new ArgumentNullException(nameof(first));
                if (second == null) throw new ArgumentNullException(nameof(second));

                return first(_value);
            }

            public override int GetHashCode() => Choice.GetHashCode(this, _value);
            public override bool Equals(object obj) => Choice.Equals(this, obj, c => c._value);
            public override bool Equals(Choice<T1, T2> other) => Choice.Equals(this, other, c => c._value);

            public override string ToString() => Choice.ToString(_value);
        }

        [DebuggerDisplay(nameof(Choice2Of2) + ": {" + nameof(_value) + "}")]
        sealed partial class Choice2Of2 : Choice<T1, T2>
        {
            readonly T2 _value;

            public Choice2Of2(T2 value) => _value = value;

            public override TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second)
            {
                if (first == null) throw new ArgumentNullException(nameof(first));
                if (second == null) throw new ArgumentNullException(nameof(second));

                return second(_value);
            }

            public override int GetHashCode() => Choice.GetHashCode(this, _value);
            public override bool Equals(object obj) => Choice.Equals(this, obj, c => c._value);
            public override bool Equals(Choice<T1, T2> other) => Choice.Equals(this, other, c => c._value);

            public override string ToString() => Choice.ToString(_value);
        }
    }

    abstract partial class Choice<T1, T2, T3> : IEquatable<Choice<T1, T2, T3>>
    {
        internal static Choice<T1, T2, T3> Choice1(T1 value) => new Choice1Of3(value);
        internal static Choice<T1, T2, T3> Choice2(T2 value) => new Choice2Of3(value);
        internal static Choice<T1, T2, T3> Choice3(T3 value) => new Choice3Of3(value);

        public abstract TResult Match<TResult>(
            Func<T1, TResult> first,
            Func<T2, TResult> second,
            Func<T3, TResult> third);

        public abstract override int GetHashCode();
        public abstract override bool Equals(object obj);
        public abstract bool Equals(Choice<T1, T2, T3> other);

        public abstract override string ToString();

        [DebuggerDisplay(nameof(Choice1Of3) + ": {" + nameof(_value) + "}")]
        sealed partial class Choice1Of3 : Choice<T1, T2, T3>
        {
            readonly T1 _value;

            public Choice1Of3(T1 value) => _value = value;

            public override TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second, Func<T3, TResult> third)
            {
                if (first == null) throw new ArgumentNullException(nameof(first));
                if (second == null) throw new ArgumentNullException(nameof(second));
                if (third == null) throw new ArgumentNullException(nameof(third));

                return first(_value);
            }

            public override int GetHashCode() => Choice.GetHashCode(this, _value);
            public override bool Equals(object obj) => Choice.Equals(this, obj, c => c._value);
            public override bool Equals(Choice<T1, T2, T3> other) => Choice.Equals(this, other, c => c._value);

            public override string ToString() => Choice.ToString(_value);
        }

        [DebuggerDisplay(nameof(Choice2Of3) + ": {" + nameof(_value) + "}")]
        sealed partial class Choice2Of3 : Choice<T1, T2, T3>
        {
            readonly T2 _value;

            public Choice2Of3(T2 value) => _value = value;

            public override TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second, Func<T3, TResult> third)
            {
                if (first == null) throw new ArgumentNullException(nameof(first));
                if (second == null) throw new ArgumentNullException(nameof(second));
                if (third == null) throw new ArgumentNullException(nameof(third));

                return second(_value);
            }

            public override int GetHashCode() => Choice.GetHashCode(this, _value);
            public override bool Equals(object obj) => Choice.Equals(this, obj, c => c._value);
            public override bool Equals(Choice<T1, T2, T3> other) => Choice.Equals(this, other, c => c._value);

            public override string ToString() => Choice.ToString(_value);
        }

        [DebuggerDisplay(nameof(Choice3Of3) + ": {" + nameof(_value) + "}")]
        sealed partial class Choice3Of3 : Choice<T1, T2, T3>
        {
            readonly T3 _value;

            public Choice3Of3(T3 value) => _value = value;

            public override TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second, Func<T3, TResult> third)
            {
                if (first == null) throw new ArgumentNullException(nameof(first));
                if (second == null) throw new ArgumentNullException(nameof(second));
                if (third == null) throw new ArgumentNullException(nameof(third));

                return third(_value);
            }

            public override int GetHashCode() => Choice.GetHashCode(this, _value);
            public override bool Equals(object obj) => Choice.Equals(this, obj, c => c._value);
            public override bool Equals(Choice<T1, T2, T3> other) => Choice.Equals(this, other, c => c._value);

            public override string ToString() => Choice.ToString(_value);
        }
    }


    abstract partial class Choice<T1, T2, T3, T4> : IEquatable<Choice<T1, T2, T3, T4>>
    {
        internal static Choice<T1, T2, T3, T4> Choice1(T1 value) => new Choice1Of4(value);
        internal static Choice<T1, T2, T3, T4> Choice2(T2 value) => new Choice2Of4(value);
        internal static Choice<T1, T2, T3, T4> Choice3(T3 value) => new Choice3Of4(value);
        internal static Choice<T1, T2, T3, T4> Choice4(T4 value) => new Choice4Of4(value);

        public abstract TResult Match<TResult>(
            Func<T1, TResult> first,
            Func<T2, TResult> second,
            Func<T3, TResult> third,
            Func<T4, TResult> fourth);

        public abstract override int GetHashCode();
        public abstract override bool Equals(object obj);
        public abstract bool Equals(Choice<T1, T2, T3, T4> other);

        public abstract override string ToString();

        [DebuggerDisplay(nameof(Choice1Of4) + ": {" + nameof(_value) + "}")]
        sealed partial class Choice1Of4 : Choice<T1, T2, T3, T4>
        {
            readonly T1 _value;

            public Choice1Of4(T1 value) => _value = value;

            public override TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second, Func<T3, TResult> third, Func<T4, TResult> fourth)
            {
                if (first == null) throw new ArgumentNullException(nameof(first));
                if (second == null) throw new ArgumentNullException(nameof(second));
                if (third == null) throw new ArgumentNullException(nameof(third));
                if (fourth == null) throw new ArgumentNullException(nameof(fourth));

                return first(_value);
            }

            public override int GetHashCode() => Choice.GetHashCode(this, _value);
            public override bool Equals(object obj) => Choice.Equals(this, obj, c => c._value);
            public override bool Equals(Choice<T1, T2, T3, T4> other) => Choice.Equals(this, other, c => c._value);

            public override string ToString() => Choice.ToString(_value);
        }

        [DebuggerDisplay(nameof(Choice2Of4) + ": {" + nameof(_value) + "}")]
        sealed partial class Choice2Of4 : Choice<T1, T2, T3, T4>
        {
            readonly T2 _value;

            public Choice2Of4(T2 value) => _value = value;

            public override TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second, Func<T3, TResult> third, Func<T4, TResult> fourth)
            {
                if (first == null) throw new ArgumentNullException(nameof(first));
                if (second == null) throw new ArgumentNullException(nameof(second));
                if (third == null) throw new ArgumentNullException(nameof(third));
                if (fourth == null) throw new ArgumentNullException(nameof(fourth));

                return second(_value);
            }

            public override int GetHashCode() => Choice.GetHashCode(this, _value);
            public override bool Equals(object obj) => Choice.Equals(this, obj, c => c._value);
            public override bool Equals(Choice<T1, T2, T3, T4> other) => Choice.Equals(this, other, c => c._value);

            public override string ToString() => Choice.ToString(_value);
        }

        [DebuggerDisplay(nameof(Choice3Of4) + ": {" + nameof(_value) + "}")]
        sealed partial class Choice3Of4 : Choice<T1, T2, T3, T4>
        {
            readonly T3 _value;

            public Choice3Of4(T3 value) => _value = value;

            public override TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second, Func<T3, TResult> third, Func<T4, TResult> fourth)
            {
                if (first == null) throw new ArgumentNullException(nameof(first));
                if (second == null) throw new ArgumentNullException(nameof(second));
                if (third == null) throw new ArgumentNullException(nameof(third));
                if (fourth == null) throw new ArgumentNullException(nameof(fourth));

                return third(_value);
            }

            public override int GetHashCode() => Choice.GetHashCode(this, _value);
            public override bool Equals(object obj) => Choice.Equals(this, obj, c => c._value);
            public override bool Equals(Choice<T1, T2, T3, T4> other) => Choice.Equals(this, other, c => c._value);

            public override string ToString() => Choice.ToString(_value);
        }

        [DebuggerDisplay(nameof(Choice4Of4) + ": {" + nameof(_value) + "}")]
        sealed partial class Choice4Of4 : Choice<T1, T2, T3, T4>
        {
            readonly T4 _value;

            public Choice4Of4(T4 value) => _value = value;

            public override TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second, Func<T3, TResult> third, Func<T4, TResult> fourth)
            {
                if (first == null) throw new ArgumentNullException(nameof(first));
                if (second == null) throw new ArgumentNullException(nameof(second));
                if (third == null) throw new ArgumentNullException(nameof(third));
                if (fourth == null) throw new ArgumentNullException(nameof(fourth));

                return fourth(_value);
            }

            public override int GetHashCode() => Choice.GetHashCode(this, _value);
            public override bool Equals(object obj) => Choice.Equals(this, obj, c => c._value);
            public override bool Equals(Choice<T1, T2, T3, T4> other) => Choice.Equals(this, other, c => c._value);

            public override string ToString() => Choice.ToString(_value);
        }
    }

    abstract partial class Choice<T1, T2, T3, T4, T5> : IEquatable<Choice<T1, T2, T3, T4, T5>>
    {
        internal static Choice<T1, T2, T3, T4, T5> Choice1(T1 value) => new Choice1Of5(value);
        internal static Choice<T1, T2, T3, T4, T5> Choice2(T2 value) => new Choice2Of5(value);
        internal static Choice<T1, T2, T3, T4, T5> Choice3(T3 value) => new Choice3Of5(value);
        internal static Choice<T1, T2, T3, T4, T5> Choice4(T4 value) => new Choice4Of5(value);
        internal static Choice<T1, T2, T3, T4, T5> Choice5(T5 value) => new Choice5Of5(value);

        public abstract TResult Match<TResult>(
            Func<T1, TResult> first,
            Func<T2, TResult> second,
            Func<T3, TResult> third,
            Func<T4, TResult> fourth,
            Func<T5, TResult> fifth);

        public abstract override int GetHashCode();
        public abstract override bool Equals(object obj);
        public abstract bool Equals(Choice<T1, T2, T3, T4, T5> other);

        public abstract override string ToString();

        [DebuggerDisplay(nameof(Choice1Of5) + ": {" + nameof(_value) + "}")]
        sealed partial class Choice1Of5 : Choice<T1, T2, T3, T4, T5>
        {
            readonly T1 _value;

            public Choice1Of5(T1 value) => _value = value;

            public override TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second, Func<T3, TResult> third, Func<T4, TResult> fourth, Func<T5, TResult> fifth)
            {
                if (first == null) throw new ArgumentNullException(nameof(first));
                if (second == null) throw new ArgumentNullException(nameof(second));
                if (third == null) throw new ArgumentNullException(nameof(third));
                if (fourth == null) throw new ArgumentNullException(nameof(fourth));
                if (fifth == null) throw new ArgumentNullException(nameof(fifth));

                return first(_value);
            }

            public override int GetHashCode() => Choice.GetHashCode(this, _value);
            public override bool Equals(object obj) => Choice.Equals(this, obj, c => c._value);
            public override bool Equals(Choice<T1, T2, T3, T4, T5> other) => Choice.Equals(this, other, c => c._value);

            public override string ToString() => Choice.ToString(_value);
        }

        [DebuggerDisplay(nameof(Choice2Of5) + ": {" + nameof(_value) + "}")]
        sealed partial class Choice2Of5 : Choice<T1, T2, T3, T4, T5>
        {
            readonly T2 _value;

            public Choice2Of5(T2 value) => _value = value;

            public override TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second, Func<T3, TResult> third, Func<T4, TResult> fourth, Func<T5, TResult> fifth)
            {
                if (first == null) throw new ArgumentNullException(nameof(first));
                if (second == null) throw new ArgumentNullException(nameof(second));
                if (third == null) throw new ArgumentNullException(nameof(third));
                if (fourth == null) throw new ArgumentNullException(nameof(fourth));
                if (fifth == null) throw new ArgumentNullException(nameof(fifth));

                return second(_value);
            }

            public override int GetHashCode() => Choice.GetHashCode(this, _value);
            public override bool Equals(object obj) => Choice.Equals(this, obj, c => c._value);
            public override bool Equals(Choice<T1, T2, T3, T4, T5> other) => Choice.Equals(this, other, c => c._value);

            public override string ToString() => Choice.ToString(_value);
        }

        [DebuggerDisplay(nameof(Choice3Of5) + ": {" + nameof(_value) + "}")]
        sealed partial class Choice3Of5 : Choice<T1, T2, T3, T4, T5>
        {
            readonly T3 _value;

            public Choice3Of5(T3 value) => _value = value;

            public override TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second, Func<T3, TResult> third, Func<T4, TResult> fourth, Func<T5, TResult> fifth)
            {
                if (first == null) throw new ArgumentNullException(nameof(first));
                if (second == null) throw new ArgumentNullException(nameof(second));
                if (third == null) throw new ArgumentNullException(nameof(third));
                if (fourth == null) throw new ArgumentNullException(nameof(fourth));
                if (fifth == null) throw new ArgumentNullException(nameof(fifth));

                return third(_value);
            }

            public override int GetHashCode() => Choice.GetHashCode(this, _value);
            public override bool Equals(object obj) => Choice.Equals(this, obj, c => c._value);
            public override bool Equals(Choice<T1, T2, T3, T4, T5> other) => Choice.Equals(this, other, c => c._value);

            public override string ToString() => Choice.ToString(_value);
        }

        [DebuggerDisplay(nameof(Choice4Of5) + ": {" + nameof(_value) + "}")]
        sealed partial class Choice4Of5 : Choice<T1, T2, T3, T4, T5>
        {
            readonly T4 _value;

            public Choice4Of5(T4 value) => _value = value;

            public override TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second, Func<T3, TResult> third, Func<T4, TResult> fourth, Func<T5, TResult> fifth)
            {
                if (first == null) throw new ArgumentNullException(nameof(first));
                if (second == null) throw new ArgumentNullException(nameof(second));
                if (third == null) throw new ArgumentNullException(nameof(third));
                if (fourth == null) throw new ArgumentNullException(nameof(fourth));
                if (fifth == null) throw new ArgumentNullException(nameof(fifth));

                return fourth(_value);
            }

            public override int GetHashCode() => Choice.GetHashCode(this, _value);
            public override bool Equals(object obj) => Choice.Equals(this, obj, c => c._value);
            public override bool Equals(Choice<T1, T2, T3, T4, T5> other) => Choice.Equals(this, other, c => c._value);

            public override string ToString() => Choice.ToString(_value);
        }

        [DebuggerDisplay(nameof(Choice3Of5) + ": {" + nameof(_value) + "}")]
        sealed partial class Choice5Of5 : Choice<T1, T2, T3, T4, T5>
        {
            readonly T5 _value;

            public Choice5Of5(T5 value) => _value = value;

            public override TResult Match<TResult>(Func<T1, TResult> first, Func<T2, TResult> second, Func<T3, TResult> third, Func<T4, TResult> fourth, Func<T5, TResult> fifth)
            {
                if (first == null) throw new ArgumentNullException(nameof(first));
                if (second == null) throw new ArgumentNullException(nameof(second));
                if (third == null) throw new ArgumentNullException(nameof(third));
                if (fourth == null) throw new ArgumentNullException(nameof(fourth));
                if (fifth == null) throw new ArgumentNullException(nameof(fifth));

                return fifth(_value);
            }

            public override int GetHashCode() => Choice.GetHashCode(this, _value);
            public override bool Equals(object obj) => Choice.Equals(this, obj, c => c._value);
            public override bool Equals(Choice<T1, T2, T3, T4, T5> other) => Choice.Equals(this, other, c => c._value);

            public override string ToString() => Choice.ToString(_value);
        }
    }
}
