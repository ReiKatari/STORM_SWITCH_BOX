using System;
using System.IO;
using LibHac.Fs;

namespace StormSwitchBox.Core.NSZ;

internal class StorageStream : Stream
{
	private readonly IStorage _storage;

	private readonly long _baseOffset;

	private readonly long _length;

	private long _position;

	public override bool CanRead => true;

	public override bool CanSeek => true;

	public override bool CanWrite => false;

	public override long Length => _length;

	public override long Position
	{
		get
		{
			return _position;
		}
		set
		{
			_position = value;
		}
	}

	public StorageStream(IStorage storage, long baseOffset, long length)
	{
		_storage = storage;
		_baseOffset = baseOffset;
		_length = length;
		_position = 0L;
	}

	public override void Flush()
	{
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		long num = _length - _position;
		if (num <= 0)
		{
			return 0;
		}
		int num2 = (int)Math.Min(count, num);
		try
		{
			_storage.Read(_baseOffset + _position, buffer.AsSpan(offset, num2)).ThrowIfFailure();
		}
		catch (Exception)
		{
			return 0;
		}
		_position += num2;
		return num2;
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		switch (origin)
		{
		case SeekOrigin.Begin:
			_position = offset;
			break;
		case SeekOrigin.Current:
			_position += offset;
			break;
		case SeekOrigin.End:
			_position = _length + offset;
			break;
		}
		return _position;
	}

	public override void SetLength(long value)
	{
		throw new NotSupportedException();
	}

	public override void Write(byte[] buffer, int offset, int count)
	{
		throw new NotSupportedException();
	}
}
