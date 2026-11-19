using System.Text;
using Tailviewer.Api;
using Tailviewer.Settings;

namespace Tailviewer.Avalonia;

// Owns raw-file documents independently of the currently visible source.
internal sealed class SourceDocuments : IDisposable
{
    private readonly object _sync = new();
    private readonly Func<DataSource, Encoding?, LogDocument> _open;
    private readonly Dictionary<DataSourceId, Entry> _entries = new();
    private bool _disposed;

    internal sealed class Entry(Task<LogDocument> opening, string path, Encoding? encoding, bool singleLine)
    {
        public readonly Task<LogDocument> Opening = opening;
        public readonly string Path = path;
        public readonly Encoding? Encoding = encoding;
        public readonly bool SingleLine = singleLine;
        public int References = 1;
        public bool Retired;
        public bool Released;
    }

    internal sealed class Lease(SourceDocuments owner, Entry entry) : IDisposable
    {
        private Entry? _entry = entry;
        public LogDocument Document => (_entry ?? throw new ObjectDisposedException(nameof(Lease))).Opening.GetAwaiter().GetResult();
        public Encoding? Encoding => (_entry ?? throw new ObjectDisposedException(nameof(Lease))).Encoding;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _entry, null) is { } held) owner.Release(held);
        }
    }

    // The factory returns a new owned document on each invocation; it runs off the calling thread.
    public SourceDocuments(Func<DataSource, Encoding?, LogDocument> open) => _open = open ?? throw new ArgumentNullException(nameof(open));

    public void Configure(IEnumerable<DataSource> sources, Encoding? encoding)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var snapshot = sources.ToArray();
        var ids = new HashSet<DataSourceId>();
        foreach (var source in snapshot)
            if (source.Id == DataSourceId.Empty || !ids.Add(source.Id))
                throw new ArgumentException("Source identities must be nonempty and unique.", nameof(sources));
        var files = snapshot.Where(source => source.File != null).Select(source =>
        {
            var candidate = source.Clone();
            candidate.File = Path.GetFullPath(candidate.File);
            return candidate;
        }).ToDictionary(source => source.Id);
        var decoder = (Encoding?)encoding?.Clone();
        var retired = new List<LogDocument>();
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            foreach (var (id, entry) in _entries.ToArray())
            {
                // shortcut: reader keys cover path/decoder/single-line mode; extend with custom parser settings in T043.
                if (files.TryGetValue(id, out var file) && SamePath(entry.Path, file.File) && Equals(entry.Encoding, decoder) &&
                    entry.SingleLine == file.IsSingleLine &&
                    !entry.Opening.IsFaulted && !entry.Opening.IsCanceled) continue;
                _entries.Remove(id);
                entry.Retired = true;
                --entry.References;
                if (TakeUnused(entry) is { } document) retired.Add(document);
            }
            foreach (var (id, file) in files)
            {
                if (_entries.ContainsKey(id)) continue;
                var entry = new Entry(Task.Run(() => _open(file, decoder) ?? throw new InvalidOperationException("The reader factory returned no document.")), file.File, decoder, file.IsSingleLine);
                _entries.Add(id, entry);
                _ = entry.Opening.ContinueWith(completed =>
                {
                    if (completed.IsFaulted) _ = completed.Exception;
                    LogDocument? unused;
                    lock (_sync) unused = TakeUnused(entry);
                    unused?.Dispose();
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        foreach (var document in retired) document.Dispose();
    }

    public async Task<Lease> AcquireAsync(DataSourceId id)
    {
        Entry entry;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_entries.TryGetValue(id, out entry!)) throw new KeyNotFoundException("The source is no longer configured.");
            ++entry.References;
        }
        try
        {
            await entry.Opening.ConfigureAwait(false);
            lock (_sync)
            {
                if (entry.Retired) throw new OperationCanceledException("The source is no longer configured.");
                return new Lease(this, entry);
            }
        }
        catch
        {
            Release(entry);
            throw;
        }
    }

    public bool TryAcquire(DataSourceId id, out Lease? lease)
    {
        lock (_sync)
        {
            if (!_disposed && _entries.TryGetValue(id, out var entry) && entry.Opening.IsCompletedSuccessfully)
            {
                ++entry.References;
                lease = new Lease(this, entry);
                return true;
            }
        }
        lease = null;
        return false;
    }

    public string? GetError(DataSourceId id)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue(id, out var entry)) return null;
            return entry.Opening.IsCanceled ? "Opening the source was cancelled." : entry.Opening.Exception?.GetBaseException().Message;
        }
    }

    private void Release(Entry entry)
    {
        LogDocument? unused;
        lock (_sync)
        {
            --entry.References;
            unused = TakeUnused(entry);
        }
        unused?.Dispose();
    }

    private static LogDocument? TakeUnused(Entry entry)
    {
        if (entry.References != 0 || entry.Released || !entry.Opening.IsCompletedSuccessfully) return null;
        entry.Released = true;
        return entry.Opening.Result;
    }

    private static bool SamePath(string left, string right) => string.Equals(left, right,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    public void Dispose()
    {
        var retired = new List<LogDocument>();
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var entry in _entries.Values)
            {
                entry.Retired = true;
                --entry.References;
                if (TakeUnused(entry) is { } document) retired.Add(document);
            }
            _entries.Clear();
        }
        foreach (var document in retired) document.Dispose();
    }
}
