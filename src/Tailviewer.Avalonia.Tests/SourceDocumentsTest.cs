using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using Tailviewer.Api;
using Tailviewer.Settings;

namespace Tailviewer.Avalonia.Tests;

[TestFixture]
public sealed class SourceDocumentsTest
{
    private string _folder = null!;
    private DataSource _source = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "tailviewer-reader-owner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _source = new DataSource(Path.Combine(_folder, "one.log")) { Id = DataSourceId.CreateNew() };
        File.WriteAllText(_source.File, "one");
    }

    [TearDown]
    public void TearDown()
    {
        Assert.That(Path.GetDirectoryName(Path.GetFullPath(_folder)),
            Is.EqualTo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))));
        Directory.Delete(_folder, recursive: true);
    }

    [Test]
    public async Task ConcurrentAcquisitionsShareOneOpenAndRetiredReadersLiveUntilTheirLastLease()
    {
        var opens = 0;
        using var owner = new SourceDocuments((source, encoding) =>
        {
            Interlocked.Increment(ref opens);
            return new LogDocument(source, encoding);
        });
        owner.Configure(new[] { _source }, null);
        var leases = await Task.WhenAll(owner.AcquireAsync(_source.Id), owner.AcquireAsync(_source.Id));
        using var first = leases[0];
        using var second = leases[1];
        var document = first.Document;
        Assert.That(second.Document, Is.SameAs(document));
        Assert.That(opens, Is.EqualTo(1));
        await Ready(document);
        Assert.That(owner.TryAcquire(_source.Id, out var cached), Is.True);
        cached!.Dispose();
        owner.Configure(Array.Empty<DataSource>(), null);
        Assert.That(owner.TryAcquire(_source.Id, out _), Is.False);
        first.Dispose();
        Assert.That(await second.Document.CopyTextAsync(new[] { 0 }), Is.EqualTo("one"));
        second.Dispose();
        Assert.CatchAsync(async () => await document.CopyTextAsync(new[] { 0 }));
    }

    [Test]
    public async Task RemovingAPendingSourceDisposesItsLateOpenInsteadOfReturningALease()
    {
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource<LogDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var owner = new SourceDocuments((source, encoding) =>
        {
            var document = new LogDocument(source, encoding);
            started.SetResult(document);
            if (!release.Wait(TimeSpan.FromSeconds(8)))
            {
                document.Dispose();
                throw new TimeoutException("Owned factory gate was not released.");
            }
            return document;
        });
        try
        {
            owner.Configure(new[] { _source }, null);
            var pending = owner.AcquireAsync(_source.Id);
            var document = await started.Task.WaitAsync(TimeSpan.FromSeconds(8));
            await Ready(document);
            Assert.That(await document.CopyTextAsync(new[] { 0 }), Is.EqualTo("one"));
            owner.Configure(Array.Empty<DataSource>(), null);
            release.Set();
            Assert.ThrowsAsync<OperationCanceledException>(async () => await pending);
            Assert.CatchAsync(async () => await document.CopyTextAsync(new[] { 0 }));
        }
        finally { release.Set(); }
    }

    [Test]
    public async Task OwnerClosureDisposesAnUnleasedLateReader()
    {
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource<LogDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var owner = new SourceDocuments((source, encoding) =>
        {
            var document = new LogDocument(source, encoding);
            started.SetResult(document);
            if (!release.Wait(TimeSpan.FromSeconds(8)))
            {
                document.Dispose();
                throw new TimeoutException("Owned factory gate was not released.");
            }
            return document;
        });
        try
        {
            owner.Configure(new[] { _source }, null);
            var document = await started.Task.WaitAsync(TimeSpan.FromSeconds(8));
            await Ready(document);
            Assert.That(await document.CopyTextAsync(new[] { 0 }), Is.EqualTo("one"));
            owner.Dispose();
            release.Set();
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < TimeSpan.FromSeconds(8))
            {
                try { await document.CopyTextAsync(new[] { 0 }); }
                catch (Exception error) when (error is IOException or ObjectDisposedException or OperationCanceledException) { return; }
                await Task.Delay(10);
            }
            Assert.Fail("The late unleased reader must be disposed after its owner closes.");
        }
        finally { release.Set(); }
    }

    [Test]
    public async Task AChangedPathReplacesTheReaderWhileAnOldLeaseRemainsReadable()
    {
        using var owner = new SourceDocuments((source, encoding) => new LogDocument(source, encoding));
        owner.Configure(new[] { _source }, null);
        using var first = await owner.AcquireAsync(_source.Id);
        await Ready(first.Document);
        _source.File = Path.Combine(_folder, "two.log");
        File.WriteAllText(_source.File, "two");
        owner.Configure(new[] { _source }, null);
        using var second = await owner.AcquireAsync(_source.Id);
        await Ready(second.Document);
        Assert.That(second.Document, Is.Not.SameAs(first.Document));
        Assert.That(await first.Document.CopyTextAsync(new[] { 0 }), Is.EqualTo("one"));
        Assert.That(await second.Document.CopyTextAsync(new[] { 0 }), Is.EqualTo("two"));
    }

    [Test]
    public async Task CanonicalPathsReuseReadersButManualEncodingChangesRetireThem()
    {
        var opens = 0;
        using var owner = new SourceDocuments((source, encoding) =>
        {
            Interlocked.Increment(ref opens);
            return new LogDocument(source, encoding);
        });
        owner.Configure(new[] { _source }, null);
        using var first = await owner.AcquireAsync(_source.Id);
        _source.File = Path.Combine(_folder, ".", "one.log");
        owner.Configure(new[] { _source }, null);
        using var same = await owner.AcquireAsync(_source.Id);
        Assert.That(same.Document, Is.SameAs(first.Document));
        owner.Configure(new[] { _source }, Encoding.UTF8);
        using var manual = await owner.AcquireAsync(_source.Id);
        Assert.That(manual.Document, Is.Not.SameAs(first.Document), "manual UTF-8 and automatic BOM detection are different choices");
        Assert.That(opens, Is.EqualTo(2));
        await Ready(first.Document);
        Assert.That(await first.Document.CopyTextAsync(new[] { 0 }), Is.EqualTo("one"));
    }

    [Test]
    public async Task FailedFactoriesAreReportedAndRetriedByTheNextConfigurationRefresh()
    {
        var opens = 0;
        using var owner = new SourceDocuments((source, encoding) => Interlocked.Increment(ref opens) == 1
            ? throw new IOException("owned open failure") : new LogDocument(source, encoding));
        owner.Configure(new[] { _source }, null);
        Assert.ThrowsAsync<IOException>(async () => await owner.AcquireAsync(_source.Id));
        Assert.That(owner.GetError(_source.Id), Does.Contain("owned open failure"));
        owner.Configure(new[] { _source }, null);
        using var recovered = await owner.AcquireAsync(_source.Id);
        await Ready(recovered.Document);
        Assert.That(owner.GetError(_source.Id), Is.Null);
        Assert.That(await recovered.Document.CopyTextAsync(new[] { 0 }), Is.EqualTo("one"));
    }

    [Test]
    public async Task InvalidSnapshotsKeepThePreviousOwnerAndClosingRejectsNewAcquisitions()
    {
        using var owner = new SourceDocuments((source, encoding) => new LogDocument(source, encoding));
        owner.Configure(new[] { _source }, null);
        using var active = await owner.AcquireAsync(_source.Id);
        Assert.Throws<ArgumentException>(() => owner.Configure(new[] { _source, _source.Clone() }, null));
        using var same = await owner.AcquireAsync(_source.Id);
        Assert.That(same.Document, Is.SameAs(active.Document));
        owner.Dispose();
        Assert.ThrowsAsync<ObjectDisposedException>(async () => await owner.AcquireAsync(_source.Id));
        await Ready(active.Document);
        Assert.That(await active.Document.CopyTextAsync(new[] { 0 }), Is.EqualTo("one"));
    }

    private static async Task Ready(LogDocument document)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(8))
        {
            document.Refresh();
            if (document.Lines.Count == 1) return;
            await Task.Delay(10);
        }
        Assert.Fail("The owned one-line fixture must finish indexing.");
    }
}
