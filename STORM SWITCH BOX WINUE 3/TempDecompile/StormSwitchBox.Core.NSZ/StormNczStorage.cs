using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LibHac;
using LibHac.Fs;
using ZstdSharp;

namespace StormSwitchBox.Core.NSZ;

public class StormNczStorage : IStorage
{
	private readonly IStorage _baseStorage;

	private long _uncompressedSize;

	private bool _isSolid;

	private List<NczSection> _sections = new List<NczSection>();

	private NczBlockHeader? _blockHeader;

	private long _zstdStreamOffset;

	private string? _tempSolidFile;

	private FileStream? _tempSolidStream;

	private readonly LruBlockCache _blockCache = new LruBlockCache(256);

	private readonly object _baseStorageLock = new object();

	private readonly IStorage? _solidStorage;

	private readonly Dictionary<string, byte[]>? _titleKeys;

	private bool _ncaHeaderInSolidStream = false;

	public long PhysicalSize { get; private set; }

	public bool IsSolid => _isSolid;

	public long UncompressedSize => _uncompressedSize;

	public Stream? TempSolidStream => _tempSolidStream;

	public StormNczStorage(IStorage baseStorage, Dictionary<string, byte[]>? titleKeys = null, IStorage? solidStorage = null)
	{
		_baseStorage = baseStorage;
		_titleKeys = titleKeys;
		_solidStorage = solidStorage;
		Initialize();
	}

	~StormNczStorage()
	{
		try
		{
			if (_tempSolidStream != null)
			{
				_tempSolidStream.Dispose();
				_tempSolidStream = null;
			}
			if (_tempSolidFile != null && File.Exists(_tempSolidFile))
			{
				File.Delete(_tempSolidFile);
			}
		}
		catch
		{
		}
	}

	private void Initialize()
	{
		long num = 16384L;
		long num2 = 0L;
		byte[] array = new byte[8];
		_baseStorage.GetSize(out var size).ThrowIfFailure();
		if (size < 8)
		{
			throw new InvalidDataException($"File is too small to contain NCZ magic! Size: {size}");
		}
		_baseStorage.Read(num2, array).ThrowIfFailure();
		string text = Encoding.ASCII.GetString(array);
		if (text != "NCZSECTN" && text != "NCZBLOCK")
		{
			num2 = 16384L;
			if (size >= num2 + 8)
			{
				_baseStorage.Read(num2, array).ThrowIfFailure();
				text = Encoding.ASCII.GetString(array);
			}
			if (text != "NCZSECTN" && text != "NCZBLOCK")
			{
				throw new InvalidDataException($"No NCZ magic found! Magic at 0x4000: {text} (BaseSize: {size})");
			}
		}
		long num3 = num;
		long num4 = 0L;
		if (text == "NCZSECTN")
		{
			if (num2 == 0)
			{
				_ncaHeaderInSolidStream = true;
			}
			num2 += 8;
			byte[] array2 = new byte[8];
			_baseStorage.Read(num2, array2);
			long num5 = BitConverter.ToInt64(array2, 0);
			num2 += 8;
			_baseStorage.GetSize(out var size2).ThrowIfFailure();
			for (int i = 0; i < num5; i++)
			{
				if (num2 + 64 > size2)
				{
					break;
				}
				byte[] array3 = new byte[64];
				_baseStorage.Read(num2, array3);
				NczSection nczSection = new NczSection();
				nczSection.Offset = BitConverter.ToInt64(array3, 0);
				nczSection.Size = BitConverter.ToInt64(array3, 8);
				nczSection.CryptoType = BitConverter.ToInt64(array3, 16);
				nczSection.CryptoKey = new byte[16];
				nczSection.CryptoCounter = new byte[16];
				nczSection.SolidStreamOffset = num4;
				NczSection nczSection2 = nczSection;
				Array.Copy(array3, 32, nczSection2.CryptoKey, 0, 16);
				Array.Copy(array3, 48, nczSection2.CryptoCounter, 0, 16);
				bool flag = true;
				for (int j = 0; j < 16; j++)
				{
					if (nczSection2.CryptoKey[j] != 0)
					{
						flag = false;
						break;
					}
				}
				if ((nczSection2.CryptoType == 2 || nczSection2.CryptoType == 3 || nczSection2.CryptoType == 4) && flag && _titleKeys != null && _titleKeys.Count > 0)
				{
					Array.Copy(_titleKeys.Values.First(), nczSection2.CryptoKey, 16);
				}
				_sections.Add(nczSection2);
				long num6 = Math.Max(0L, nczSection2.Offset + nczSection2.Size - Math.Max(nczSection2.Offset, 16384L));
				num4 += num6;
				long num7 = nczSection2.Offset + nczSection2.Size;
				if (num7 > num3)
				{
					num3 = num7;
				}
				num2 += 64;
			}
			byte[] array4 = new byte[1024];
			_baseStorage.Read(num2, array4);
			int num8 = -1;
			byte[] bytes = Encoding.ASCII.GetBytes("NCZBLOCK");
			for (int k = 0; k < array4.Length - 8; k++)
			{
				bool flag2 = true;
				for (int l = 0; l < 8; l++)
				{
					if (array4[k + l] != bytes[l])
					{
						flag2 = false;
						break;
					}
				}
				if (flag2)
				{
					num8 = k;
					break;
				}
			}
			if (num8 != -1)
			{
				text = "NCZBLOCK";
				num2 += num8;
			}
			else
			{
				_isSolid = true;
				byte[] array5 = new byte[4] { 40, 181, 47, 253 };
				int num9 = -1;
				for (int m = 0; m < array4.Length - 4; m++)
				{
					if (array4[m] == array5[0] && array4[m + 1] == array5[1] && array4[m + 2] == array5[2] && array4[m + 3] == array5[3])
					{
						num9 = m;
						break;
					}
				}
				if (num9 != -1)
				{
					num2 += num9;
				}
			}
		}
		if (text == "NCZBLOCK")
		{
			_isSolid = false;
			num2 += 8;
			byte[] array6 = new byte[16];
			_baseStorage.Read(num2, array6);
			num2 += 16;
			_blockHeader = new NczBlockHeader
			{
				Version = array6[0],
				Type = array6[1],
				Unused = array6[2],
				BlockSizeExponent = array6[3],
				NumberOfBlocks = BitConverter.ToInt32(array6, 4),
				DecompressedSize = BitConverter.ToInt64(array6, 8)
			};
			num3 = _blockHeader.DecompressedSize;
			byte[] array7 = new byte[_blockHeader.NumberOfBlocks * 4];
			_baseStorage.Read(num2, array7);
			num2 += array7.Length;
			long num10 = num2;
			for (int n = 0; n < _blockHeader.NumberOfBlocks; n++)
			{
				int num11 = BitConverter.ToInt32(array7, n * 4);
				_blockHeader.CompressedBlockSizeList.Add(num11);
				_blockHeader.CompressedBlockOffsetList.Add(num10);
				num10 += num11;
			}
			PhysicalSize = num10;
		}
		_zstdStreamOffset = num2;
		_uncompressedSize = num3;
		if (!_isSolid)
		{
			return;
		}
		_tempSolidFile = System.IO.Path.GetTempFileName();
		IStorage storage = _solidStorage ?? _baseStorage;
		storage.GetSize(out var size3).ThrowIfFailure();
		long num12 = ((_solidStorage != null) ? 0 : _zstdStreamOffset);
		long length = size3 - num12;
		PhysicalSize = size3;
		using (StorageStream stream = new StorageStream(storage, num12, length))
		{
			using DecompressionStream decompressionStream = new DecompressionStream(stream);
			using FileStream destination = new FileStream(_tempSolidFile, FileMode.Create, FileAccess.Write, FileShare.None);
			try
			{
				decompressionStream.CopyTo(destination);
			}
			catch (Exception ex)
			{
				string message = $"ZstdSharp failed to decompress solid stream. Offset: {num12}. Error: {ex.Message}";
				throw new InvalidDataException(message, ex);
			}
		}
		_tempSolidStream = new FileStream(_tempSolidFile, FileMode.Open, FileAccess.Read, FileShare.Read);
	}

	public override Result Read(long offset, Span<byte> destination)
	{
		long num = 16384L;
		int num2 = 0;
		long num3 = offset;
		int num4 = destination.Length;
		while (num4 > 0)
		{
			if (num3 < num)
			{
				int num5 = (int)Math.Min(num4, num - num3);
				lock (_baseStorageLock)
				{
					try
					{
						Span<byte> destination2 = destination.Slice(num2, num5);
						if (_isSolid && _ncaHeaderInSolidStream)
						{
							_tempSolidStream.Position = num3;
							byte[] array = new byte[num5];
							_tempSolidStream.Read(array, 0, num5);
							array.AsSpan().CopyTo(destination2);
						}
						else
						{
							_baseStorage.Read(num3, destination2);
						}
					}
					catch (Exception)
					{
					}
				}
				num2 += num5;
				num3 += num5;
				num4 -= num5;
				continue;
			}
			NczSection nczSection = null;
			if (_sections.Count > 0)
			{
				foreach (NczSection section in _sections)
				{
					if (num3 >= section.Offset && num3 < section.Offset + section.Size)
					{
						nczSection = section;
						break;
					}
				}
				if (nczSection == null)
				{
					long num6 = _uncompressedSize;
					foreach (NczSection section2 in _sections)
					{
						if (section2.Offset > num3 && section2.Offset < num6)
						{
							num6 = section2.Offset;
						}
					}
					int num7 = (int)Math.Min(num4, num6 - num3);
					if (num7 <= 0)
					{
						break;
					}
					destination.Slice(num2, num7).Fill(0);
					num2 += num7;
					num3 += num7;
					num4 -= num7;
					continue;
				}
			}
			long num8 = ((nczSection != null) ? (num3 - nczSection.Offset) : 0);
			long num9 = ((nczSection != null) ? (num3 - Math.Max(nczSection.Offset, 16384L)) : 0);
			long num10 = ((nczSection != null) ? (nczSection.SolidStreamOffset + num9) : num3);
			long val = ((nczSection != null) ? (nczSection.Size - num8) : (_uncompressedSize - num3));
			if (_isSolid)
			{
				lock (_baseStorageLock)
				{
					_tempSolidStream.Position = num10;
					long num11 = _tempSolidStream.Length - num10;
					int num12 = (int)Math.Min(num4, Math.Min(val, (num11 > 0) ? num11 : 0));
					if (num12 <= 0)
					{
						break;
					}
					byte[] array2 = new byte[num12];
					_tempSolidStream.Read(array2, 0, num12);
					if (nczSection != null && (nczSection.CryptoType == 2 || nczSection.CryptoType == 3 || nczSection.CryptoType == 4))
					{
						AesCtrXor(array2, 0, num12, nczSection.CryptoKey, nczSection.CryptoCounter, num3, nczSection.Offset);
					}
					try
					{
						array2.AsSpan().CopyTo(destination.Slice(num2, num12));
					}
					catch (ArgumentOutOfRangeException innerException)
					{
						throw new Exception($"CRITICAL SLICE ERROR: destLen={destination.Length}, bytesRead={num2}, toReadSolid={num12}, remaining={num4}, mappedOffset={num10}, streamLen={_tempSolidStream.Length}", innerException);
					}
					num2 += num12;
					num3 += num12;
					num4 -= num12;
					continue;
				}
			}
			long num13 = 1L << (int)_blockHeader.BlockSizeExponent;
			long num14 = (_blockHeader.DecompressedSize + num13 - 1) / num13;
			long num15 = 0L;
			foreach (NczSection section3 in _sections)
			{
				long num16 = Math.Max(0L, section3.Offset + section3.Size - Math.Max(section3.Offset, 16384L));
				num15 += (num16 + num13 - 1) / num13;
			}
			bool flag = _blockHeader.NumberOfBlocks == num15 && num15 != num14;
			int num17 = -1;
			long num18 = 0L;
			if (!flag)
			{
				num17 = (int)(num10 / num13);
				num18 = num10 % num13;
			}
			else
			{
				long num19 = 0L;
				int num20 = 0;
				foreach (NczSection section4 in _sections)
				{
					long num21 = Math.Max(0L, section4.Offset + section4.Size - Math.Max(section4.Offset, 16384L));
					long num22 = (num21 + num13 - 1) / num13;
					if (num10 >= num19 && num10 < num19 + num21)
					{
						long num23 = num10 - num19;
						num17 = num20 + (int)(num23 / num13);
						num18 = num23 % num13;
						break;
					}
					num19 += num21;
					num20 += (int)num22;
				}
			}
			if (num17 == -1 || num17 >= _blockHeader.NumberOfBlocks)
			{
				break;
			}
			int num24 = (int)Math.Min(num4, Math.Min(val, num13 - num18));
			byte[] array3 = _blockCache.Get(num17);
			if (array3 == null)
			{
				long num25 = _blockHeader.CompressedBlockOffsetList[num17];
				int num26 = _blockHeader.CompressedBlockSizeList[num17];
				_baseStorage.GetSize(out var size).ThrowIfFailure();
				if (num25 + num26 > size)
				{
					num26 = (int)Math.Max(0L, size - num25);
				}
				byte[] array4 = new byte[num26];
				lock (_baseStorageLock)
				{
					if (num26 > 0)
					{
						try
						{
							_baseStorage.Read(num25, array4);
						}
						catch (Exception)
						{
							Array.Clear(array4, 0, array4.Length);
						}
					}
				}
				long num27 = num13;
				if (!flag)
				{
					if (num17 == _blockHeader.NumberOfBlocks - 1)
					{
						num27 = _blockHeader.DecompressedSize - num17 * num13;
					}
				}
				else
				{
					int num28 = 0;
					foreach (NczSection section5 in _sections)
					{
						long num29 = Math.Max(0L, section5.Offset + section5.Size - Math.Max(section5.Offset, 16384L));
						long num30 = (num29 + num13 - 1) / num13;
						if (num17 >= num28 && num17 < num28 + num30)
						{
							if (num17 == num28 + num30 - 1)
							{
								num27 = num29 % num13;
								if (num27 == 0)
								{
									num27 = num13;
								}
							}
							break;
						}
						num28 += (int)num30;
					}
				}
				if (num27 <= 0)
				{
					num27 = num13;
				}
				array3 = new byte[(int)num27];
				try
				{
					using Decompressor decompressor = new Decompressor();
					decompressor.Unwrap(new ReadOnlySpan<byte>(array4), new Span<byte>(array3));
				}
				catch (Exception ex3)
				{
					throw new Exception($"ZstdSharp failed to decompress block {num17}. Compressed size: {num26}. Offset: {num25}. Error: {ex3.Message}", ex3);
				}
				_blockCache.Put(num17, array3);
			}
			byte[] array5 = new byte[num24];
			Array.Copy(array3, (int)num18, array5, 0, num24);
			if (nczSection != null && (nczSection.CryptoType == 2 || nczSection.CryptoType == 3 || nczSection.CryptoType == 4))
			{
				AesCtrXor(array5, 0, num24, nczSection.CryptoKey, nczSection.CryptoCounter, nczSection.Offset + num8, nczSection.Offset);
			}
			new Span<byte>(array5, 0, num24).CopyTo(destination.Slice(num2, num24));
			num2 += num24;
			num3 += num24;
			num4 -= num24;
		}
		if (num4 > 0)
		{
			destination.Slice(num2, num4).Fill(0);
		}
		return Result.Success;
	}

	private void AesCtrXor(byte[] data, int dataOffset, int length, byte[] key, byte[] iv, long globalOffset, long originalSectionOffset = 0L)
	{
		if (globalOffset < 512 && length > 0)
		{
			App.Logger.Log($"[AesCtrXor Debug] Key={BitConverter.ToString(key).Replace("-", "")}, IV={BitConverter.ToString(iv).Replace("-", "")}, GlobalOffset={globalOffset}, SectionOffset={originalSectionOffset}");
		}
		using Aes aes = Aes.Create();
		aes.Mode = CipherMode.ECB;
		aes.Padding = PaddingMode.None;
		aes.Key = key;
		using ICryptoTransform cryptoTransform = aes.CreateEncryptor();
		byte[] array = new byte[16];
		byte[] array2 = new byte[16];
		long num = globalOffset / 16;
		int num2 = (int)(globalOffset % 16);
		for (int i = 0; i < length; i++)
		{
			if (i == 0 || num2 == 0)
			{
				Array.Copy(iv, array, 16);
				AddCounter(array, num);
				cryptoTransform.TransformBlock(array, 0, 16, array2, 0);
			}
			data[dataOffset + i] ^= array2[num2];
			num2++;
			if (num2 == 16)
			{
				num2 = 0;
				num++;
			}
		}
	}

	private void AddCounter(byte[] counter, long add)
	{
		ulong num = 0uL;
		for (int i = 0; i < 8; i++)
		{
			num = (num << 8) | counter[8 + i];
		}
		num += (ulong)add;
		for (int num2 = 7; num2 >= 0; num2--)
		{
			counter[8 + num2] = (byte)(num & 0xFF);
			num >>= 8;
		}
	}

	public override Result Write(long offset, ReadOnlySpan<byte> source)
	{
		return ResultFs.NotImplemented.Value;
	}

	public override Result Flush()
	{
		return Result.Success;
	}

	public override Result SetSize(long size)
	{
		return ResultFs.NotImplemented.Value;
	}

	public override Result GetSize(out long size)
	{
		size = _uncompressedSize;
		return Result.Success;
	}

	public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
	{
		return Result.Success;
	}
}
