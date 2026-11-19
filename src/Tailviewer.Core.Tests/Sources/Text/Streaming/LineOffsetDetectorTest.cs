using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using NUnit.Framework;

namespace Tailviewer.Core.Tests.Sources.Text.Streaming
{
	[TestFixture]
	public sealed class LineOffsetDetectorTest
	{
		private MemoryStream _stream;

		public static IReadOnlyList<Encoding> Encodings
		{
			get
			{
				return new[]
				{
					Encoding.Default,
					Encoding.ASCII,
					Encoding.BigEndianUnicode,
					Encoding.UTF32,
					Encoding.UTF8,
					Encoding.UTF7
				};
			}
		}

		[SetUp]
		public void Setup()
		{
			_stream = new MemoryStream();
		}

		[Test]
		public void TestEmpty([ValueSource(nameof(Encodings))] Encoding encoding)
		{
			var detector = new LineOffsetDetector(_stream, encoding);
			detector.FindNextLineOffset().Should().Be(-1);
		}

		[Test]
		public void TestOneCharacter([ValueSource(nameof(Encodings))] Encoding encoding)
		{
			var writer = new StreamWriter(_stream, encoding);
			var line = "A";
			writer.Write(line);
			writer.Flush();
			_stream.Position = 0;

			var detector = new LineOffsetDetector(_stream, encoding);

			detector.FindNextLineOffset().Should().Be(-1, "because we're at the end and there's no newlines");

			detector.FindNextLineOffset().Should().Be(-1, "because we're at the end and there's no newlines");
		}

		[TestCase(1200, 4096)]
		[TestCase(1201, 4096)]
		[TestCase(12000, 4096)]
		[TestCase(12001, 4096)]
		[TestCase(1200, 3)]
		[TestCase(1201, 3)]
		[TestCase(12000, 3)]
		[TestCase(12001, 3)]
		public void NewlinePatternsMustStartAtACodeUnitBoundary(int codePage, int bufferSize)
		{
			var encoding = Encoding.GetEncoding(codePage);
			const string prefix = "\u0A00\u0100\u0100\u0A00";
			using (var writer = new StreamWriter(_stream, encoding, 128, leaveOpen: true))
				writer.Write(prefix + "\nlast");
			_stream.Position = 0;
			var detector = new LineOffsetDetector(_stream, encoding, bufferSize);
			Assert.That(detector.FindNextLineOffset(), Is.EqualTo(encoding.GetPreamble().Length + encoding.GetByteCount(prefix + "\n")));
			Assert.That(detector.FindNextLineOffset(), Is.EqualTo(-1));
		}

		[Test]
		public void TestOneNewline([ValueSource(nameof(Encodings))] Encoding encoding)
		{
			var writer = new StreamWriter(_stream, encoding);
			var line = "\n";
			writer.Write(line);
			writer.Flush();
			_stream.Position = 0;

			var preamble = encoding.GetPreamble();
			var detector = new LineOffsetDetector(_stream, encoding);

			var offset = detector.FindNextLineOffset();
			offset.Should().Be(preamble.Length + encoding.GetByteCount(line));
			
			detector.FindNextLineOffset().Should().Be(-1, "because we're at the end and there's no newlines anymore");
		}

		[Test]
		public void TestSentenceWithNewline([ValueSource(nameof(Encodings))] Encoding encoding)
		{
			var writer = new StreamWriter(_stream, encoding);
			var line = "This is a lazy sentence\n";
			writer.Write(line);
			writer.Flush();
			_stream.Position = 0;

			var preamble = encoding.GetPreamble();
			var detector = new LineOffsetDetector(_stream, encoding);
			var offset = detector.FindNextLineOffset();
			offset.Should().Be(preamble.Length + encoding.GetByteCount(line));
			
			detector.FindNextLineOffset().Should().Be(-1, "because we're at the end and there's no newlines anymore");
		}

		[Test]
		public void TestThreeLines([ValueSource(nameof(Encodings))] Encoding encoding,
		                           [Values(1, 2, 4, 8, 16)] int bufferSize)
		{
			var writer = new StreamWriter(_stream, encoding, bufferSize);
			var line1 = "This\n";
			var line2 = "is\n";
			var line3 = "crazy";
			writer.Write(line1);
			writer.Write(line2);
			writer.Write(line3);
			writer.Flush();
			_stream.Position = 0;

			var preamble = encoding.GetPreamble();
			var detector = new LineOffsetDetector(_stream, encoding);
			var offset1 = detector.FindNextLineOffset();
			offset1.Should().Be(preamble.Length + encoding.GetByteCount(line1));

			var offset2 = detector.FindNextLineOffset();
			offset2.Should().Be(offset1 + encoding.GetByteCount(line2));
			
			detector.FindNextLineOffset().Should().Be(-1, "because we're at the end and there's no newlines anymore");
		}
	}
}
