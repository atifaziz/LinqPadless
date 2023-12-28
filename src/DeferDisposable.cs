namespace LinqPadless
{
    using System;

    // Credit Stuart Lang and source:
    // https://stu.dev/defer-with-csharp8/

    static class DeferDisposable
    {
        public static DeferDisposable<T> Defer<T>(T arg, Action<T> action) => new(arg, action);
    }

    readonly struct DeferDisposable<T> : IDisposable
    {
        readonly Action<T> action;
        readonly T arg;

        public DeferDisposable(T arg, Action<T> action) =>
            (this.action, this.arg) = (action, arg);
        public void Dispose() => this.action.Invoke(this.arg);
    }
}
