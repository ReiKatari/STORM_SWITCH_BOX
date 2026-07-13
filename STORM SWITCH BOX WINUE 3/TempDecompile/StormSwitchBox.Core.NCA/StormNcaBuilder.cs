using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.RomFs;
using StormSwitchBox.Services;

namespace StormSwitchBox.Core.NCA;

public class StormNcaBuilder
{
	private class NcaSectionEntry
	{
		public long StartBlock;

		public long EndBlock;

		public byte[]? Hash = null;

		public NcaSectionEntry(byte[] header, int offset)
		{
			StartBlock = BitConverter.ToUInt32(header, offset);
			EndBlock = BitConverter.ToUInt32(header, offset + 4);
		}

		public void Update(byte[] header, int offset, byte[] hash)
		{
			Array.Copy(BitConverter.GetBytes((uint)StartBlock), 0, header, offset, 4);
			Array.Copy(BitConverter.GetBytes((uint)EndBlock), 0, header, offset + 4, 4);
			int num = (offset - 576) / 16;
			int destinationIndex = 640 + num * 32;
			Array.Copy(hash, 0, header, destinationIndex, 32);
		}
	}

	private class SubStream : Stream
	{
		private readonly Stream _baseStream;

		private readonly long _start;

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

		public SubStream(Stream baseStream, long start, long length)
		{
			_baseStream = baseStream;
			_start = start;
			_length = length;
			_position = 0L;
		}

		public override void Flush()
		{
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (_position >= _length)
			{
				return 0;
			}
			long num = Math.Min(count, _length - _position);
			_baseStream.Position = _start + _position;
			int num2 = _baseStream.Read(buffer, offset, (int)num);
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

	private readonly KeysService _keys;

	public StormNcaBuilder(KeysService keys)
	{
		_keys = keys;
	}

	public async Task BuildProgramNcaAsync(string titleIdHex, string controlNcaPath, string exefsDir, string romfsDir, string outNcaPath)
	{
		KeySet keySet = _keys.CurrentKeyset;
		byte[] headerEncrypted = new byte[3072];
		using FileStream file = new FileStream(controlNcaPath, FileMode.Open, FileAccess.Read);
		file.Read(headerEncrypted, 0, 3072);
		byte[] headerDecrypted = new byte[3072];
		byte[] headerKey = new byte[32];
		((ReadOnlySpan<byte>)keySet.HeaderKey).CopyTo(headerKey);
		if (headerEncrypted[512] == 78 && headerEncrypted[513] == 67 && headerEncrypted[514] == 65 && headerEncrypted[515] == 51)
		{
			Array.Copy(headerEncrypted, headerDecrypted, 3072);
		}
		else
		{
			XtsDecrypt(headerEncrypted, headerDecrypted, headerKey, 512, 0);
		}
		if (headerDecrypted[512] != 78 || headerDecrypted[513] != 67 || headerDecrypted[514] != 65 || headerDecrypted[515] != 51)
		{
			string encHex = BitConverter.ToString(headerEncrypted, 512, 16);
			string decHex = BitConverter.ToString(headerDecrypted, 512, 16);
			throw new Exception("Неверный формат базового NCA (ожидался NCA3). Убедитесь, что prod.keys актуальны.\nEncrypted Magic: " + encHex + "\nDecrypted Magic: " + decHex);
		}
		for (int i = 560; i < 576; i++)
		{
			headerDecrypted[i] = 0;
		}
		int cryptoType = headerDecrypted[518];
		byte[] areaKey = new byte[16];
		((ReadOnlySpan<byte>)keySet.KeyAreaKeys[cryptoType][2]).CopyTo(areaKey);
		IStorage romfsStorage = new MemoryStorage(new byte[0]);
		long romfsSize = 0L;
		if (!string.IsNullOrEmpty(romfsDir) && Directory.Exists(romfsDir))
		{
			RomFsBuilder builder = new RomFsBuilder(new LocalFileSystem(romfsDir));
			romfsStorage = builder.Build();
			romfsStorage.GetSize(out romfsSize).ThrowIfFailure();
		}
		IStorage exefsStorage = new MemoryStorage(new byte[0]);
		long exefsSize = 0L;
		if (!string.IsNullOrEmpty(exefsDir) && Directory.Exists(exefsDir))
		{
			PartitionFileSystemBuilder pfsBuilder = new PartitionFileSystemBuilder();
			string[] files = Directory.GetFiles(exefsDir);
			foreach (string exefile in files)
			{
				pfsBuilder.AddFile(System.IO.Path.GetFileName(exefile), new LocalFile(exefile, OpenMode.Read));
			}
			exefsStorage = pfsBuilder.Build(PartitionFileSystemType.Standard);
			exefsStorage.GetSize(out exefsSize).ThrowIfFailure();
		}
		NcaSectionEntry[] sections = new NcaSectionEntry[4];
		for (int k = 0; k < 4; k++)
		{
			sections[k] = new NcaSectionEntry(headerDecrypted, 576 + k * 16);
		}
		long currentBlock = 6L;
		using FileStream outFile = new FileStream(outNcaPath, FileMode.Create, FileAccess.Write);
		outFile.SetLength(3072L);
		outFile.Position = 3072L;
		for (int l = 0; l < 4; l++)
		{
			if (sections[l].StartBlock == 0L && sections[l].EndBlock == 0)
			{
				continue;
			}
			int fsHeaderOffset = 1024 + l * 512;
			byte[] fsHeader = new byte[512];
			Array.Copy(headerDecrypted, fsHeaderOffset, fsHeader, 0, 512);
			byte[] generation = new byte[8];
			Array.Copy(fsHeader, 320, generation, 0, 8);
			long newSize;
			Stream sourceStream;
			if (l == 0 && exefsSize > 0)
			{
				newSize = exefsSize;
				MemoryStream tempStream = new MemoryStream();
				new SafeStorageWrapper(exefsStorage).AsStream().CopyTo(tempStream);
				tempStream.Position = 0L;
				sourceStream = tempStream;
				using SHA256 sha = SHA256.Create();
				tempStream.Position = 0L;
				byte[] hash = sha.ComputeHash(tempStream);
				Array.Copy(hash, 0, fsHeader, 8, 32);
				tempStream.Position = 0L;
			}
			else if (l == 1 && romfsSize > 0)
			{
				newSize = romfsSize;
				string tempRomfs = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(outNcaPath), "temp_romfs.bin");
				using (FileStream rStream = new FileStream(tempRomfs, FileMode.Create, FileAccess.ReadWrite))
				{
					new SafeStorageWrapper(romfsStorage).AsStream().CopyTo(rStream);
					var (newMasterHash, _) = StormIvfcBuilder.BuildIvfc(rStream);
					Array.Copy(newMasterHash, 0, fsHeader, 200, 32);
				}
				sourceStream = new FileStream(tempRomfs, FileMode.Open, FileAccess.Read);
			}
			else
			{
				long oldSize = (sections[l].EndBlock - sections[l].StartBlock) * 512;
				newSize = oldSize;
				sourceStream = new SubStream(file, sections[l].StartBlock * 512, oldSize);
			}
			long startBlock = currentBlock;
			long endBlock = startBlock + (newSize + 511) / 512;
			sections[l].StartBlock = startBlock;
			sections[l].EndBlock = endBlock;
			currentBlock = endBlock;
			using (SHA256 sha2 = SHA256.Create())
			{
				byte[] fsHash = sha2.ComputeHash(fsHeader);
				sections[l].Update(headerDecrypted, 576 + l * 16, fsHash);
			}
			Array.Copy(fsHeader, 0, headerDecrypted, fsHeaderOffset, 512);
			WriteEncryptedSection(sourceStream, outFile, areaKey, generation, 0L, newSize);
			sourceStream.Dispose();
		}
		byte[] newHeaderEncrypted = new byte[3072];
		XtsEncrypt(headerDecrypted, newHeaderEncrypted, headerKey, 512, 0);
		outFile.Position = 0L;
		outFile.Write(newHeaderEncrypted, 0, 3072);
	}

	private void WriteEncryptedSection(Stream source, Stream dest, byte[] areaKey, byte[] generation, long offsetBase, long length)
	{
		byte[] array = new byte[16384];
		long num = length;
		long num2 = offsetBase;
		byte[] array2 = new byte[16];
		Array.Copy(generation, 0, array2, 0, 8);
		while (num > 0)
		{
			int count = (int)Math.Min(16384L, num);
			int num3 = source.Read(array, 0, count);
			if (num3 <= 0)
			{
				break;
			}
			long value = num2 / 16;
			byte[] bytes = BitConverter.GetBytes(value);
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(bytes);
			}
			Array.Copy(bytes, 0, array2, 8, 8);
			AesCtrXor(array, 0, num3, areaKey, array2, num2);
			dest.Write(array, 0, num3);
			num2 += num3;
			num -= num3;
		}
		long num4 = (512 - num2 % 512) % 512;
		if (num4 > 0)
		{
			dest.Write(new byte[num4], 0, (int)num4);
		}
	}

	public static void AesCtrXor(byte[] data, int dataOffset, int length, byte[] key, byte[] iv, long globalOffset)
	{
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
				AddCounter(array, num - globalOffset / 16);
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

	private static void AddCounter(byte[] counter, long add)
	{
		long num = add;
		int num2 = 15;
		while (num2 >= 0 && num > 0)
		{
			long num3 = counter[num2] + num;
			counter[num2] = (byte)(num3 & 0xFF);
			num = num3 >> 8;
			num2--;
		}
	}

	private static void XtsDecrypt(byte[] src, byte[] dst, byte[] key, int sectorSize, int startSector)
	{
		byte[] array = new byte[16];
		byte[] array2 = new byte[16];
		Array.Copy(key, 0, array, 0, 16);
		Array.Copy(key, 16, array2, 0, 16);
		Array.Copy(src, 0, dst, 0, 512);
		Aes128XtsTransform aes128XtsTransform = new Aes128XtsTransform(array, array2, decrypting: true);
		int num = src.Length - 512;
		Array.Copy(src, 512, dst, 512, num);
		for (int i = 0; i < num / sectorSize; i++)
		{
			aes128XtsTransform.TransformBlock(dst, 512 + i * sectorSize, sectorSize, (ulong)(startSector + 1 + i));
		}
	}

	private static void XtsEncrypt(byte[] src, byte[] dst, byte[] key, int sectorSize, int startSector)
	{
		byte[] array = new byte[16];
		byte[] array2 = new byte[16];
		Array.Copy(key, 0, array, 0, 16);
		Array.Copy(key, 16, array2, 0, 16);
		Array.Copy(src, 0, dst, 0, 512);
		Aes128XtsTransform aes128XtsTransform = new Aes128XtsTransform(array, array2, decrypting: false);
		int num = src.Length - 512;
		Array.Copy(src, 512, dst, 512, num);
		for (int i = 0; i < num / sectorSize; i++)
		{
			aes128XtsTransform.TransformBlock(dst, 512 + i * sectorSize, sectorSize, (ulong)(startSector + 1 + i));
		}
	}
}
