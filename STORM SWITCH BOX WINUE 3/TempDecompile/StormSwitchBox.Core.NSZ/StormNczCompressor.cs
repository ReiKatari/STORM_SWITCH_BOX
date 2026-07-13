using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Microsoft.UI.Dispatching;
using StormSwitchBox.Models;
using ZstdSharp;

namespace StormSwitchBox.Core.NSZ;

public static class StormNczCompressor
{
	private class SectionDecryptInfo
	{
		public long Offset { get; set; }

		public long Size { get; set; }

		public byte EncryptionType { get; set; }

		public byte[] CryptoKey { get; set; }

		public ulong BaseUpperIv { get; set; }

		public bool IsSparse { get; set; }

		public long SparsePhysicalOffset { get; set; }

		public long SparseBucketStart { get; set; }

		public long SparseBucketEnd { get; set; }

		public ulong SparseUpperIv { get; set; }
	}

	private const long NCA_HEADER_SIZE = 16384L;

	private const int BLOCK_SIZE_EXPONENT = 18;

	private const int BLOCK_SIZE = 262144;

	public static void CompressNcaToNcz(IStorage ncaStorage, string outputNczPath, int compressionLevel, KeySet keyset, ProcessingTask task, CancellationToken cancellationToken)
	{
		ncaStorage.GetSize(out var size).ThrowIfFailure();
		if (size <= 16384)
		{
			using (FileStream destination = new FileStream(outputNczPath, FileMode.Create, FileAccess.Write))
			{
				ncaStorage.AsStream().CopyTo(destination);
				return;
			}
		}
		Nca nca;
		try
		{
			nca = new Nca(keyset, ncaStorage);
		}
		catch
		{
			using FileStream destination2 = new FileStream(outputNczPath, FileMode.Create, FileAccess.Write);
			ncaStorage.AsStream().CopyTo(destination2);
			return;
		}
		using FileStream fileStream = new FileStream(outputNczPath, FileMode.Create, FileAccess.ReadWrite);
		byte[] array = new byte[16384];
		ncaStorage.Read(0L, array).ThrowIfFailure();
		fileStream.Write(array, 0, array.Length);
		List<NczSection> list = new List<NczSection>();
		byte[] array2 = new byte[3072];
		byte[] array3 = new byte[3072];
		Array.Copy(array, array3, 3072);
		byte[] array4 = new byte[32];
		((ReadOnlySpan<byte>)keyset.HeaderKey).CopyTo(array4);
		if (array3[512] == 78 && array3[513] == 67 && array3[514] == 65 && array3[515] == 51)
		{
			Array.Copy(array3, array2, 3072);
		}
		else
		{
			byte[] array5 = new byte[16];
			byte[] array6 = new byte[16];
			Array.Copy(array4, 0, array5, 0, 16);
			Array.Copy(array4, 16, array6, 0, 16);
			Array.Copy(array3, 0, array2, 0, 512);
			Aes128XtsTransform aes128XtsTransform = new Aes128XtsTransform(array5, array6, decrypting: true);
			int num = 2560;
			Array.Copy(array3, 512, array2, 512, num);
			for (int i = 0; i < num / 512; i++)
			{
				aes128XtsTransform.TransformBlock(array2, 512 + i * 512, 512, (ulong)(1 + i));
			}
		}
		List<SectionDecryptInfo> list2 = new List<SectionDecryptInfo>();
		NcaHeader header;
		for (int j = 0; j < 4; j++)
		{
			header = nca.Header;
			if (!header.IsSectionEnabled(j))
			{
				continue;
			}
			uint num2 = BitConverter.ToUInt32(array2, 576 + j * 16);
			uint num3 = BitConverter.ToUInt32(array2, 576 + j * 16 + 4);
			byte[] array7 = new byte[512];
			Array.Copy(array2, 1024 + j * 512, array7, 0, 512);
			byte b = array7[4];
			byte[] array8 = new byte[16];
			if (b == 2 || b == 3 || b == 4)
			{
				Array.Copy(array7, 320, array8, 0, 16);
			}
			byte[] array9 = new byte[16];
			if (b != 1)
			{
				header = nca.Header;
				if (header.HasRightsId)
				{
					try
					{
						header = nca.Header;
						GetTitleKeyFromKeyset(keyset, header.RightsId.ToArray())?.CopyTo(array9);
					}
					catch
					{
					}
				}
				else
				{
					try
					{
						nca.GetDecryptedKey(2).CopyTo(array9);
					}
					catch
					{
					}
				}
			}
			ulong num4 = BitConverter.ToUInt64(array7, 320);
			ushort num5 = BitConverter.ToUInt16(array7, 368);
			bool flag = num5 != 0;
			long num6 = 0L;
			long num7 = 0L;
			long sparseBucketEnd = 0L;
			ulong sparseUpperIv = 0uL;
			if (flag)
			{
				num6 = BitConverter.ToInt64(array7, 360);
				long num8 = BitConverter.ToInt64(array7, 328);
				long num9 = BitConverter.ToInt64(array7, 336);
				num7 = num6 + num8;
				sparseBucketEnd = num7 + num9;
				uint num10 = (uint)(num4 >> 32);
				sparseUpperIv = ((ulong)num10 << 32) | ((ulong)num5 << 16);
			}
			list2.Add(new SectionDecryptInfo
			{
				Offset = (long)num2 * 512L,
				Size = (long)(num3 - num2) * 512L,
				EncryptionType = b,
				CryptoKey = array9,
				BaseUpperIv = num4,
				IsSparse = flag,
				SparsePhysicalOffset = num6,
				SparseBucketStart = num7,
				SparseBucketEnd = sparseBucketEnd,
				SparseUpperIv = sparseUpperIv
			});
			list.Add(new NczSection
			{
				Offset = (long)num2 * 512L,
				Size = (long)(num3 - num2) * 512L,
				CryptoType = ((b == 1) ? 1 : b),
				CryptoKey = array9,
				CryptoCounter = array8,
				SolidStreamOffset = j
			});
		}
		byte[] bytes = Encoding.ASCII.GetBytes("NCZSECTN");
		fileStream.Write(bytes, 0, 8);
		fileStream.Write(BitConverter.GetBytes((long)list.Count), 0, 8);
		foreach (NczSection item in list)
		{
			fileStream.Write(BitConverter.GetBytes(item.Offset), 0, 8);
			fileStream.Write(BitConverter.GetBytes(item.Size), 0, 8);
			fileStream.Write(BitConverter.GetBytes(item.CryptoType), 0, 8);
			fileStream.Write(new byte[8], 0, 8);
			header = nca.Header;
			if (header.HasRightsId)
			{
				fileStream.Write(new byte[16], 0, 16);
			}
			else
			{
				fileStream.Write(item.CryptoKey, 0, 16);
			}
			fileStream.Write(item.CryptoCounter, 0, 16);
		}
		long num11 = size - 16384;
		int blocksToCompress = (int)(num11 / 262144 + ((num11 % 262144 > 0) ? 1 : 0));
		byte[] bytes2 = Encoding.ASCII.GetBytes("NCZBLOCK");
		fileStream.Write(bytes2, 0, 8);
		fileStream.WriteByte(2);
		fileStream.WriteByte(1);
		fileStream.WriteByte(0);
		fileStream.WriteByte(18);
		fileStream.Write(BitConverter.GetBytes(blocksToCompress), 0, 4);
		fileStream.Write(BitConverter.GetBytes(num11), 0, 8);
		long position = fileStream.Position;
		fileStream.Write(new byte[blocksToCompress * 4], 0, blocksToCompress * 4);
		int num12 = 0;
		try
		{
			string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ssb_native.settings.json");
			if (File.Exists(path))
			{
				string json = File.ReadAllText(path);
				using JsonDocument jsonDocument = JsonDocument.Parse(json);
				if (jsonDocument.RootElement.TryGetProperty("UsedCores", out var value))
				{
					num12 = value.GetInt32();
				}
			}
		}
		catch
		{
		}
		int num13 = Math.Max(1, (num12 > 0) ? num12 : Environment.ProcessorCount);
		int num14 = num13 * 4;
		int[] array10 = new int[blocksToCompress];
		long num15 = 16384L;
		long totalCompressedSize = 0L;
		DateTime utcNow = DateTime.UtcNow;
		long num16 = 0L;
		byte[][] preallocatedRaw = new byte[num14][];
		for (int k = 0; k < num14; k++)
		{
			preallocatedRaw[k] = new byte[262144];
		}
		ThreadLocal<Compressor> threadCompressor = new ThreadLocal<Compressor>(() => new Compressor(compressionLevel), trackAllValues: true);
		try
		{
			for (int batchStart = 0; batchStart < blocksToCompress; batchStart += num14)
			{
				cancellationToken.ThrowIfCancellationRequested();
				int currentBatchCount = Math.Min(num14, blocksToCompress - batchStart);
				int[] currentRawSizes = new int[currentBatchCount];
				byte[][] batchCompressedBlocks = new byte[currentBatchCount][];
				for (int num17 = 0; num17 < currentBatchCount; num17++)
				{
					int num18 = (int)Math.Min(262144L, size - num15);
					currentRawSizes[num17] = num18;
					ncaStorage.Read(num15, new Span<byte>(preallocatedRaw[num17], 0, num18)).ThrowIfFailure();
					for (int num19 = 0; num19 < list2.Count; num19++)
					{
						SectionDecryptInfo sectionDecryptInfo = list2[num19];
						if (sectionDecryptInfo.EncryptionType != 2 && sectionDecryptInfo.EncryptionType != 3 && sectionDecryptInfo.EncryptionType != 4)
						{
							continue;
						}
						long num20 = Math.Max(num15, sectionDecryptInfo.Offset);
						long num21 = Math.Min(num15 + num18, sectionDecryptInfo.Offset + sectionDecryptInfo.Size);
						if (num20 < num21)
						{
							int length = (int)(num21 - num20);
							int blockOffset = (int)(num20 - num15);
							try
							{
								DecryptSectionRegion(preallocatedRaw[num17], blockOffset, length, num20, sectionDecryptInfo);
							}
							catch
							{
							}
						}
					}
					num15 += num18;
				}
				Parallel.For(0, currentBatchCount, new ParallelOptions
				{
					MaxDegreeOfParallelism = num13
				}, delegate(int num24)
				{
					Compressor value3 = threadCompressor.Value;
					if (value3 != null)
					{
						ReadOnlySpan<byte> src = new ReadOnlySpan<byte>(preallocatedRaw[num24], 0, currentRawSizes[num24]);
						byte[] array12 = value3.Wrap(src).ToArray();
						if (array12.Length < currentRawSizes[num24])
						{
							batchCompressedBlocks[num24] = array12;
						}
						else
						{
							batchCompressedBlocks[num24] = src.ToArray();
						}
					}
				});
				for (int num22 = 0; num22 < currentBatchCount; num22++)
				{
					byte[] array11 = batchCompressedBlocks[num22];
					fileStream.Write(array11, 0, array11.Length);
					array10[batchStart + num22] = array11.Length;
					totalCompressedSize += array11.Length;
				}
				long ticks = DateTime.UtcNow.Ticks;
				if (!((double)(ticks - num16) > 10000000.0) && batchStart + num14 < blocksToCompress)
				{
					continue;
				}
				num16 = ticks;
				double totalSeconds = (DateTime.UtcNow - utcNow).TotalSeconds;
				double mbPerSec = (double)(num15 - 16384) / 1024.0 / 1024.0 / ((totalSeconds > 0.0) ? totalSeconds : 1.0);
				double progress = (double)num15 / (double)size * 100.0;
				bool flag2 = false;
				try
				{
					Type type = Type.GetType("StormSwitchBox.App, StormSwitchBox");
					if (type != null)
					{
						PropertyInfo property = type.GetProperty("MainDispatcher", BindingFlags.Static | BindingFlags.Public);
						if (property != null)
						{
							object value2 = property.GetValue(null);
							if (value2 != null)
							{
								MethodInfo method = value2.GetType().GetMethod("TryEnqueue", new Type[1] { typeof(DispatcherQueueHandler) });
								if (method != null)
								{
									Action action = delegate
									{
										task.Progress = progress;
										task.Speed = $"{mbPerSec:F1} MB/s";
										task.LogDetails = $"[Parallel] {batchStart + currentBatchCount}/{blocksToCompress} блоков\nСкорость: {mbPerSec:F2} MB/s\nСжато: {(double)totalCompressedSize / 1024.0 / 1024.0:F1} MB";
									};
									Type parameterType = method.GetParameters()[0].ParameterType;
									Delegate obj6 = Delegate.CreateDelegate(parameterType, action.Target, action.Method);
									method.Invoke(value2, new object[1] { obj6 });
									flag2 = true;
								}
							}
						}
					}
				}
				catch
				{
				}
				if (!flag2)
				{
					task.Progress = progress;
					task.Speed = $"{mbPerSec:F1} MB/s";
					task.LogDetails = $"[Parallel] {batchStart + currentBatchCount}/{blocksToCompress} блоков\nСкорость: {mbPerSec:F2} MB/s\nСжато: {(double)totalCompressedSize / 1024.0 / 1024.0:F1} MB";
				}
			}
			foreach (Compressor value4 in threadCompressor.Values)
			{
				value4?.Dispose();
			}
		}
		finally
		{
			if (threadCompressor != null)
			{
				((IDisposable)threadCompressor).Dispose();
			}
		}
		long position2 = fileStream.Position;
		fileStream.Seek(position, SeekOrigin.Begin);
		for (int num23 = 0; num23 < blocksToCompress; num23++)
		{
			fileStream.Write(BitConverter.GetBytes(array10[num23]), 0, 4);
		}
		fileStream.Seek(position2, SeekOrigin.Begin);
	}

	private static void DecryptSectionRegion(byte[] blockData, int blockOffset, int length, long globalOffset, SectionDecryptInfo sec)
	{
		if (sec.IsSparse)
		{
			long num = globalOffset;
			int num2 = length;
			int num3 = blockOffset;
			while (num2 > 0)
			{
				if (num >= sec.SparseBucketStart && num < sec.SparseBucketEnd)
				{
					int num4 = (int)Math.Min(num2, sec.SparseBucketEnd - num);
					AesCtrXorDirect(blockData, num3, num4, sec.CryptoKey, sec.SparseUpperIv, num);
					num += num4;
					num2 -= num4;
					num3 += num4;
					continue;
				}
				long num5 = sec.SparseBucketStart;
				if (num >= sec.SparseBucketEnd)
				{
					num5 = sec.Offset + sec.Size;
				}
				int num6 = (int)Math.Min(num2, num5 - num);
				AesCtrXorDirect(blockData, num3, num6, sec.CryptoKey, sec.BaseUpperIv, num);
				num += num6;
				num2 -= num6;
				num3 += num6;
			}
		}
		else
		{
			AesCtrXorDirect(blockData, blockOffset, length, sec.CryptoKey, sec.BaseUpperIv, globalOffset);
		}
	}

	private static void AesCtrXorDirect(byte[] data, int dataOffset, int length, byte[] key, ulong upperIv, long globalOffset)
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
		ulong num3 = upperIv;
		for (int i = 0; i < 8; i++)
		{
			array[7 - i] = (byte)(num3 & 0xFF);
			num3 >>= 8;
		}
		ulong num4 = (ulong)num;
		for (int j = 0; j < 8; j++)
		{
			array[15 - j] = (byte)(num4 & 0xFF);
			num4 >>= 8;
		}
		for (int k = 0; k < length; k++)
		{
			if (k == 0 || num2 == 0)
			{
				if (k > 0)
				{
					num++;
					num4 = (ulong)num;
					for (int l = 0; l < 8; l++)
					{
						array[15 - l] = (byte)(num4 & 0xFF);
						num4 >>= 8;
					}
				}
				cryptoTransform.TransformBlock(array, 0, 16, array2, 0);
			}
			data[dataOffset + k] ^= array2[num2];
			num2++;
			if (num2 == 16)
			{
				num2 = 0;
			}
		}
	}

	private static byte[] GetTitleKeyFromKeyset(KeySet keyset, byte[] targetRightsIdBytes)
	{
		try
		{
			PropertyInfo property = keyset.GetType().GetProperty("ExternalKeySet", BindingFlags.Instance | BindingFlags.Public);
			if (property == null)
			{
				return null;
			}
			object value = property.GetValue(keyset);
			if (value == null)
			{
				return null;
			}
			PropertyInfo property2 = value.GetType().GetProperty("ExternalKeys", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property2 == null)
			{
				return null;
			}
			if (!(property2.GetValue(value) is IEnumerable enumerable))
			{
				return null;
			}
			foreach (object item in enumerable)
			{
				Type type = item.GetType();
				PropertyInfo property3 = type.GetProperty("Key");
				PropertyInfo property4 = type.GetProperty("Value");
				if (!(property3 != null) || !(property4 != null))
				{
					continue;
				}
				object value2 = property3.GetValue(item);
				object value3 = property4.GetValue(item);
				if (value2 == null || value3 == null)
				{
					continue;
				}
				byte[] array = ExtractBytesFromRightsId(value2);
				if (array == null)
				{
					continue;
				}
				bool flag = true;
				for (int i = 0; i < 16; i++)
				{
					if (array[i] != targetRightsIdBytes[i])
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					return ExtractBytesFromAccessKey(value3);
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static byte[] ExtractBytesFromRightsId(object keyObj)
	{
		try
		{
			FieldInfo field = keyObj.GetType().GetField("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field == null)
			{
				return null;
			}
			object value = field.GetValue(keyObj);
			if (value == null)
			{
				return null;
			}
			byte[] array = new byte[16];
			Type type = value.GetType();
			for (int i = 0; i < 16; i++)
			{
				FieldInfo field2 = type.GetField($"Element{i}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field2 != null)
				{
					array[i] = (byte)field2.GetValue(value);
				}
			}
			return array;
		}
		catch
		{
			string text = keyObj.ToString()?.Replace("-", "").Trim().ToLowerInvariant();
			if (!string.IsNullOrEmpty(text) && text.Length == 32)
			{
				byte[] array2 = new byte[16];
				for (int j = 0; j < 16; j++)
				{
					array2[j] = Convert.ToByte(text.Substring(j * 2, 2), 16);
				}
				return array2;
			}
		}
		return null;
	}

	private static byte[] ExtractBytesFromAccessKey(object valueObj)
	{
		try
		{
			PropertyInfo property = valueObj.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
			if (property != null)
			{
				object value = property.GetValue(valueObj);
				if (value != null)
				{
					MethodInfo method = value.GetType().GetMethod("ToArray");
					if (method != null)
					{
						return method.Invoke(value, null) as byte[];
					}
				}
			}
		}
		catch
		{
		}
		try
		{
			FieldInfo field = valueObj.GetType().GetField("Key", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				object value2 = field.GetValue(valueObj);
				if (value2 != null)
				{
					byte[] array = new byte[16];
					Type type = value2.GetType();
					for (int i = 0; i < 16; i++)
					{
						FieldInfo field2 = type.GetField($"Element{i}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (field2 != null)
						{
							array[i] = (byte)field2.GetValue(value2);
						}
					}
					return array;
				}
			}
		}
		catch
		{
		}
		return null;
	}
}
