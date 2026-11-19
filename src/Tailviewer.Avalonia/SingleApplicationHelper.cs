using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Tailviewer.Avalonia;

internal sealed class SingleApplicationHelper : IDisposable
{
    private static readonly object Sync = new();
    private static readonly HashSet<string> OwnedNames = new(StringComparer.Ordinal);
    private readonly Mutex _mutex;
    private readonly string _name;
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private bool _disposed;

    private SingleApplicationHelper(string name, Mutex mutex) { _name = name; _mutex = mutex; }

    public static string GetChannelName()
    {
        using var process = Process.GetCurrentProcess();
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            return GetChannelName(identity.User?.Value ?? throw new InvalidOperationException("Windows user identity is unavailable."), process.SessionId);
        }
        // shortcut: Windows user/session behavior is the first milestone; Unix desktop sessions need native acceptance later.
        return GetChannelName(Environment.UserName, 0);
    }

    internal static string GetChannelName(string user, int session) =>
        "Tailviewer.Avalonia.v1." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user + "\n" + session.ToString(CultureInfo.InvariantCulture))));

    public static SingleApplicationHelper? TryAcquire(string name)
    {
        lock (Sync)
        {
            // Named mutexes are recursive on a thread; a second helper must still be secondary.
            if (OwnedNames.Contains(name)) return null;
            var mutex = new Mutex(false, name, new NamedWaitHandleOptions { CurrentUserOnly = true, CurrentSessionOnly = true });
            try
            {
                try
                {
                    if (!mutex.WaitOne(0)) { mutex.Dispose(); return null; }
                }
                catch (AbandonedMutexException) { /* The caller now owns the abandoned mutex. */ }
                OwnedNames.Add(name);
                return new SingleApplicationHelper(name, mutex);
            }
            catch { mutex.Dispose(); throw; }
        }
    }

    public void Dispose()
    {
        lock (Sync)
        {
            if (_disposed) return;
            if (Environment.CurrentManagedThreadId != _ownerThread)
                throw new InvalidOperationException("Single-instance ownership must be released on its acquiring thread.");
            _mutex.ReleaseMutex();
            _mutex.Dispose();
            OwnedNames.Remove(_name);
            _disposed = true;
        }
    }
}
