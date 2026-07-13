using System.Collections.Generic;

namespace StormSwitchBox.Core.NSZ;

public class NczBlockHeader
{
	public byte Version { get; set; }

	public byte Type { get; set; }

	public byte Unused { get; set; }

	public byte BlockSizeExponent { get; set; }

	public int NumberOfBlocks { get; set; }

	public long DecompressedSize { get; set; }

	public List<int> CompressedBlockSizeList { get; set; } = new List<int>();

	public List<long> CompressedBlockOffsetList { get; set; } = new List<long>();
}
