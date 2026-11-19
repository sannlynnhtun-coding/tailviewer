using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using FluentAssertions;
using NUnit.Framework;

namespace Tailviewer.Avalonia.Tests;

[TestFixture]
public sealed class SingleApplicationMessageTest
{
    [Test]
    public async Task RelativeUnicodeSourcesBecomeAbsoluteAndRetainTheirOrder()
    {
        using var stream = new MemoryStream();
        var paths = new[] { "မြန်မာ 😀.log", "folder with spaces", "မြန်မာ 😀.log" };
        await SingleApplicationMessage.WriteAsync(stream, paths, CancellationToken.None);
        stream.Position = 0;
        (await SingleApplicationMessage.ReadAsync(stream, CancellationToken.None))
            .Should().Equal(paths.Select(Path.GetFullPath));
    }

    [Test]
    public async Task EmptySourcesRepresentBringToFront()
    {
        using var stream = new MemoryStream();
        await SingleApplicationMessage.WriteAsync(stream, Array.Empty<string>(), CancellationToken.None);
        stream.Position = 0;
        (await SingleApplicationMessage.ReadAsync(stream, CancellationToken.None)).Should().BeEmpty();
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MaxValue)]
    [TestCase(SingleApplicationMessage.MaximumBytes + 1)]
    public void InvalidDeclaredLengthsAreRejectedBeforeReadingPayload(int length)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, length);
        using var stream = new MemoryStream(header);
        Assert.ThrowsAsync<InvalidDataException>(() => SingleApplicationMessage.ReadAsync(stream, CancellationToken.None));
        stream.Position.Should().Be(4);
    }

    [TestCase("null")]
    [TestCase("{}")]
    [TestCase("{\"Version\":2,\"Paths\":[]}")]
    [TestCase("{\"Version\":1,\"Paths\":null}")]
    [TestCase("{\"Version\":1,\"Paths\":[null]}")]
    [TestCase("{\"Version\":1,\"Paths\":[\"relative.log\"]}")]
    [TestCase("{\"Version\":1,\"Paths\":[],\"Command\":\"unsupported\"}")]
    [TestCase("{\"Version\":1,\"Paths\":[]} trailing")]
    public void MalformedOrUnsupportedRequestsAreRejected(string json)
    {
        using var stream = Frame(Encoding.UTF8.GetBytes(json));
        Assert.ThrowsAsync<InvalidDataException>(() => SingleApplicationMessage.ReadAsync(stream, CancellationToken.None));
    }

    [TestCase(0)]
    [TestCase(3)]
    [TestCase(5)]
    public void TruncatedFramesFailWithoutPublishingAPartialRequest(int bytes)
    {
        using var frame = Frame(Encoding.UTF8.GetBytes("{\"Version\":1,\"Paths\":[]}"));
        using var stream = new MemoryStream(frame.ToArray()[..bytes]);
        Assert.ThrowsAsync<EndOfStreamException>(() => SingleApplicationMessage.ReadAsync(stream, CancellationToken.None));
    }

    [Test]
    public void IncomingSourceCountAndPathLimitsAreValidated()
    {
        foreach (var paths in new[]
                 {
                     new[] { Path.GetFullPath("valid.log") + "\0hidden" },
                     new[] { Path.GetFullPath("valid.log") + new string('x', 32768) },
                     Enumerable.Repeat(Path.GetFullPath("valid.log"), SingleApplicationMessage.MaximumPaths + 1).ToArray()
                 })
        {
            var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { Version = 1, Paths = paths });
            using var stream = Frame(payload);
            Assert.ThrowsAsync<InvalidDataException>(() => SingleApplicationMessage.ReadAsync(stream, CancellationToken.None));
        }
    }

    [Test]
    public void InvalidOutgoingPathsDoNotWriteAPrefix()
    {
        foreach (var paths in new[]
                 {
                     new[] { "valid.log", "" }, new[] { "a\0b" }, new[] { new string('x', 32768) },
                     Enumerable.Repeat("valid.log", SingleApplicationMessage.MaximumPaths + 1).ToArray(),
                     Enumerable.Repeat(new string('x', 32767), 9).ToArray(),
                     Enumerable.Repeat(new string('\u1000', 10000), 5).ToArray()
                 })
        {
            using var stream = new MemoryStream();
            Assert.ThrowsAsync<InvalidDataException>(() => SingleApplicationMessage.WriteAsync(stream, paths, CancellationToken.None));
            stream.Length.Should().Be(0);
        }
    }

    [Test]
    public async Task RealCurrentUserPipeTransfersARequestAndCancelsAnIncompleteNextFrame()
    {
        var name = "tailviewer-handoff-test-" + Guid.NewGuid().ToString("N");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        var accepted = server.WaitForConnectionAsync(deadline.Token);
        await client.ConnectAsync(deadline.Token);
        await accepted;
        var incoming = SingleApplicationMessage.ReadAsync(server, deadline.Token);
        await SingleApplicationMessage.WriteAsync(client, new[] { "မြန်မာ 😀.log" }, deadline.Token);
        (await incoming).Should().Equal(Path.GetFullPath("မြန်မာ 😀.log"));
        using var cancelled = new CancellationTokenSource();
        var incomplete = SingleApplicationMessage.ReadAsync(server, cancelled.Token);
        await client.WriteAsync(new byte[] { 32, 0 }, deadline.Token);
        await client.FlushAsync(deadline.Token);
        cancelled.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await incomplete);
    }

    private static MemoryStream Frame(byte[] payload)
    {
        var stream = new MemoryStream();
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        stream.Write(header);
        stream.Write(payload);
        stream.Position = 0;
        return stream;
    }
}
