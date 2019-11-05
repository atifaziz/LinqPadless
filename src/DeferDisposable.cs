namespace LinqPadless
{
    using System;

    // Credit Stuart Lang and source:
    // https://stu.dev/defer-with-csharp8/

    static class DeferDisposable
    {
        public static DeferDisposable<T> Defer<T>(T arg, Action<T> action) =>
            new DeferDisposable<T>(arg, action);
    }

    readonly struct DeferDisposable<T> : IDisposable
    {
        readonly Action<T> _action;
        readonly T _arg;

        public DeferDisposable(T arg, Action<T> action) =>
            (_action, _arg) = (action, arg);
        public void Dispose() => _action.Invoke(_arg);
    }
}
