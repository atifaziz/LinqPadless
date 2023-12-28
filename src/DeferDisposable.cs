namespace LinqPadless
{
    using System;

    // Credit Stuart Lang and source:
    // https://stu.dev/defer-with-csharp8/

    static class DeferDisposable
    {
        public static DeferDisposable<T> Defer<T>(T arg, Action<T> action) => new(arg, action);
    }

    readonly struct DeferDisposable<T>(T arg, Action<T> action) : IDisposable
    {
        public void Dispose() => action.Invoke(arg);
    }
}
