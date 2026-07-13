using System;
using LibHac;
using LibHac.Fs;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services;

public class SafeStorageWrapper : IStorage
{
	private readonly IStorage _baseStorage;

	public SafeStorageWrapper(IStorage baseStorage)
	{
		_baseStorage = baseStorage;
	}

	public override Result Read(long offset, Span<byte> destination)
	{
		try
		{
			long size = 0L;
			if (_baseStorage.GetSize(out size).IsSuccess())
			{
				if (offset >= size)
				{
					destination.Fill(0);
					return Result.Success;
				}
				if (offset + destination.Length > size)
				{
					int num = (int)(size - offset);
					Span<byte> destination2 = destination.Slice(0, num);
					Result result = _baseStorage.Read(offset, destination2);
					if (result.IsFailure())
					{
						return result;
					}
					destination.Slice(num).Fill(0);
					return Result.Success;
				}
			}
			return _baseStorage.Read(offset, destination);
		}
		catch (Exception ex)
		{
			long size2 = 0L;
			try
			{
				_baseStorage.GetSize(out size2);
			}
			catch
			{
			}
			if (offset >= size2)
			{
				destination.Fill(0);
				return Result.Success;
			}
			App.Logger.Log($"[SafeStorageWrapper] КРИТИЧЕСКАЯ ОШИБКА ЧТЕНИЯ (смещение {offset}, размер {size2}): {ex.Message}", LogLevel.Error);
			return ResultFs.OutOfRange.Log();
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
