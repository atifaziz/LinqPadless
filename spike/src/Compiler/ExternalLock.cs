namespace WebLinqPadQueryCompiler
{
    using System;
    using System.Threading;

    // Licensed under CC BY-SA 3.0
    // https://creativecommons.org/licenses/by-sa/3.0/
    //
    // Credit & inspiration:
    // - https://stackoverflow.com/a/229567/6682
    // - https://stackoverflow.com/a/7810107/6682

    static partial class ExternalLock
    {
        public static IDisposable EnterLocal(string name, TimeSpan? timeout) =>
            Acquire(name, timeout);

        public static IDisposable EnterGlobal(string name, TimeSpan? timeout) =>
            Acquire(Global(name) , timeout);

        static IDisposable Acquire(string name, TimeSpan? timeout)
            => TryAcquire(name, timeout, out var @lock) ? @lock
             : throw new TimeoutException("Timeout waiting for exclusive access to lock expired.");

        public static bool TryEnterLocal(string name, TimeSpan timeout, out IDisposable mutex) =>
            TryAcquire(name, timeout, out mutex);

        public static bool TryEnterGlobal(string name, TimeSpan timeout, out IDisposable mutex) =>
            TryAcquire(Global(name), timeout, out mutex);

        static string Global(string name) => @"Global\" + name;

        static bool TryAcquire(string name, TimeSpan? timeout, out IDisposable @lock)
        {
            var mutex = new Mutex(false, name, out _);
            var acquired = false;

            try
            {
                try
                {
                    acquired = mutex.WaitOne(timeout ?? Timeout.InfiniteTimeSpan, false);
                    if (!acquired)
                    {
                        @lock = default;
                        return false;
                    }
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }

                @lock = new Lock(mutex);
                mutex = null;
                return true;
            }
            finally
            {
                if (mutex != null)
                {
                    if (acquired)
                        mutex.ReleaseMutex();
                    mutex.Dispose();
                }
            }
        }

        sealed class Lock : IDisposable
        {
            Mutex _mutex;

            public Lock(Mutex mutex) =>
                _mutex = mutex ?? throw new ArgumentNullException(nameof(mutex));

            public void Dispose()
            {
                var mutex = _mutex;
                if (mutex == null)
                    return;
                _mutex = null;
                mutex.ReleaseMutex();
                mutex.Close();
            }
        }
    }
}
