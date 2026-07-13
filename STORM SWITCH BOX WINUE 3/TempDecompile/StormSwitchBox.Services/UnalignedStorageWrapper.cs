using System;
using System.Buffers;
using LibHac;
using LibHac.Fs;

namespace StormSwitchBox.Services;

public class UnalignedStorageWrapper : IStorage
{
	private readonly IStorage _baseStorage;

	public UnalignedStorageWrapper(IStorage baseStorage)
	{
		_baseStorage = baseStorage;
	}

	public override Result Read(long offset, Span<byte> destination)
	{
		long num = offset & -16;
		long num2 = offset - num;
		int num3 = (int)((destination.Length + num2 + 15) & -16);
		if (num == offset && num3 == destination.Length)
		{
			return _baseStorage.Read(offset, destination);
		}
		byte[] array = ArrayPool<byte>.Shared.Rent(num3);
		try
		{
			Result result = _baseStorage.Read(num, array.AsSpan(0, num3));
			if (result.IsFailure())
			{
				return result;
			}
			array.AsSpan((int)num2, destination.Length).CopyTo(destination);
			return Result.Success;
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(array);
		}
	}

	public override Result Write(long offset, ReadOnlySpan<byte> source)
	{
		return _baseStorage.Write(offset, source);
	}

	public override Result Flush()
	{
		return _baseStorage.Flush();
	}

	public override Result SetSize(long size)
	{
		return _baseStorage.SetSize(size);
	}

	public override Result GetSize(out long size)
	{
		return _baseStorage.GetSize(out size);
	}

	public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
	{
		return _baseStorage.OperateRange(outBuffer, operationId, offset, size, inBuffer);
	}
}
