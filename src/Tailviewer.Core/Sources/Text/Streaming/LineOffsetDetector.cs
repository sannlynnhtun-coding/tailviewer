using System.IO;
using System.Text;
using System;

// ReSharper disable once CheckNamespace
namespace Tailviewer.Core
{
	/// <summary>
	/// 
	/// </summary>
	internal sealed class LineOffsetDetector
	{
		private readonly Stream _stream;
		private readonly byte[] _pattern;
		private readonly byte[] _buffer;
		private readonly long _endOffset;
		private readonly Action<byte[], int> _observe;
		private long _bufferOffset;
		private int _bufferPos;
		private int _bufferLength;
		private int _numMatchedInSequence;

		public LineOffsetDetector(Stream stream, Encoding encoding, int bufferSize = 4096, long endOffset = long.MaxValue,
		                          Action<byte[], int> observe = null)
		{
			_stream = stream;
			_endOffset = endOffset;
			_observe = observe;
			_pattern = encoding.GetBytes("\n");
			//var pattern = encoding.GetBytes("\n");
			//if (pattern.Length > 1)
			//	throw new NotImplementedException($"Line offset detection not implemented for {encoding.WebName}");
			//_pattern = pattern[0];

			_buffer = new byte[bufferSize];
			FillBufferIfNecessary();
		}

		/// <summary>
		///    Returns the number of bytes to the start of the stream where the next line occurs.
		/// </summary>
		/// <returns></returns>
		public long FindNextLineOffset()
		{
			if (_pattern.Length == 1)
			{
				while (true)
				{
					FillBufferIfNecessary();
					if (_bufferLength == 0) return -1;
					var next = Array.IndexOf(_buffer, _pattern[0], _bufferPos, _bufferLength - _bufferPos);
					if (next >= 0)
					{
						_bufferPos = next + 1;
						return _bufferOffset + _bufferPos;
					}
					_bufferPos = _bufferLength;
				}
			}
			do
			{
				FillBufferIfNecessary();

				for (; _bufferPos < _bufferLength; ++_bufferPos)
				{
					// UTF-16/32 newline bytes inside adjacent code units are not line separators.
					if (_pattern.Length > 1 && _numMatchedInSequence == 0 && (_bufferOffset + _bufferPos) % _pattern.Length != 0)
						continue;
					if (_buffer[_bufferPos] == _pattern[_numMatchedInSequence])
					{
						++_numMatchedInSequence;

						if (_numMatchedInSequence == _pattern.Length)
						{
							++_bufferPos;
							_numMatchedInSequence = 0;
							return _bufferOffset + _bufferPos;
						}
					}
					else
					{
						_numMatchedInSequence = 0;
					}
				}
			} while (_bufferLength > 0);

			return -1;
		}

		private void FillBufferIfNecessary()
		{
			if (_bufferPos < _bufferLength)
				return;

			_bufferOffset = _stream.Position;
			_bufferLength = _bufferOffset >= _endOffset ? 0 :
				_stream.Read(_buffer, 0, (int)System.Math.Min(_buffer.Length, _endOffset - _bufferOffset));
			if (_bufferLength > 0) _observe?.Invoke(_buffer, _bufferLength);
			_bufferPos = 0;
		}
	}
}
