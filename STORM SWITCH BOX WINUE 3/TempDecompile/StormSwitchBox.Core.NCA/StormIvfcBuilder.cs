using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace StormSwitchBox.Core.NCA;

public class StormIvfcBuilder
{
	private const int BlockSize = 16384;

	public static (byte[] MasterHash, Stream IvfcStream) BuildIvfc(Stream romFsData)
	{
		List<byte[]> list = new List<byte[]>();
		romFsData.Position = 0L;
		long length = romFsData.Length;
		int num = (int)((length + 16384 - 1) / 16384);
		byte[] array = new byte[num * 32];
		using SHA256 sHA = SHA256.Create();
		byte[] array2 = new byte[16384];
		for (int i = 0; i < num; i++)
		{
			int num2 = romFsData.Read(array2, 0, 16384);
			if (num2 < 16384)
			{
				Array.Clear(array2, num2, 16384 - num2);
			}
			byte[] sourceArray = sHA.ComputeHash(array2, 0, 16384);
			Array.Copy(sourceArray, 0, array, i * 32, 32);
		}
		list.Add(array);
		byte[] array3 = array;
		for (int num3 = 3; num3 >= 1; num3--)
		{
			int num4 = (array3.Length + 16384 - 1) / 16384;
			byte[] array4 = new byte[num4 * 32];
			for (int j = 0; j < num4; j++)
			{
				int num5 = j * 16384;
				int length2 = Math.Min(16384, array3.Length - num5);
				Array.Clear(array2, 0, 16384);
				Array.Copy(array3, num5, array2, 0, length2);
				byte[] sourceArray2 = sHA.ComputeHash(array2, 0, 16384);
				Array.Copy(sourceArray2, 0, array4, j * 32, 32);
			}
			list.Insert(0, array4);
			array3 = array4;
		}
		Array.Clear(array2, 0, 16384);
		Array.Copy(array3, 0, array2, 0, array3.Length);
		byte[] item = sHA.ComputeHash(array2, 0, 16384);
		MemoryStream memoryStream = new MemoryStream();
		foreach (byte[] item2 in list)
		{
			memoryStream.Write(item2, 0, item2.Length);
			long num6 = memoryStream.Length % 16384;
			if (num6 != 0)
			{
				memoryStream.Write(new byte[16384 - num6], 0, (int)(16384 - num6));
			}
		}
		memoryStream.Position = 0L;
		return (MasterHash: item, IvfcStream: memoryStream);
	}
}
