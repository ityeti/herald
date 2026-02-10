using Serilog;

namespace Herald.Util;

/// <summary>
/// Ensures only one instance of Herald runs at a time.
/// Uses a named system mutex, matching the Python CreateMutexW approach.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private Mutex? _mutex;
    private bool _owned;

    public bool TryAcquire()
    {
        _mutex = new Mutex(true, @"Global\HeraldSingleInstance", out _owned);
        if (!_owned)
        {
            Log.Warning("Another instance of Herald is already running");
            _mutex.Dispose();
            _mutex = null;
        }
        return _owned;
    }

    public void Dispose()
    {
        if (_mutex != null && _owned)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
