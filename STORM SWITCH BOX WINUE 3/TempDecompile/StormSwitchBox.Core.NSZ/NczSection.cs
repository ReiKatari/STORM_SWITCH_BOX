using System;

namespace StormSwitchBox.Core.NSZ;

public class NczSection
{
	public long Offset { get; set; }

	public long Size { get; set; }

	public long CryptoType { get; set; }

	public byte[] CryptoKey { get; set; } = Array.Empty<byte>();

	public byte[] CryptoCounter { get; set; } = Array.Empty<byte>();

	public long SolidStreamOffset { get; set; }
}
