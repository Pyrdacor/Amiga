using System.ComponentModel;

namespace Amiga.FileFormats.LHA;

internal class Decompressor
{
	public static readonly Dictionary<string, CompressionMethod> SupportedCompressions = new()
	{
		{ "-lh0-", CompressionMethod.None },
		{ "-lh5-", CompressionMethod.LH5 },
		{ "-lh6-", CompressionMethod.LH6 },
		{ "-lh7-", CompressionMethod.LH7 },
		{ "-lz5-", CompressionMethod.LZ5 },
	};

	private readonly CRC crc = new();
	private readonly BinaryReader reader;
	private readonly CompressionMethod method;
	private readonly uint rawSize;
	private int bitCount = 0;
	private ushort bitBuf = 0;
	private byte subBitBuf = 0;
	private int compsize = 0;
	private int loc = 0;

	private const int LZHuff5DictBits = 13;
	private const int LZHuff6DictBits = 15;
	private const int LZHuff7DictBits = 16;
	private const int LZArc5DictBits = 12;
	private const int Threshold = 3;
	private const int EOF = -1;

	public Decompressor(BinaryReader reader, CompressionMethod method, uint rawSize)
	{
		this.reader = reader;
		this.method = method;
		this.rawSize = rawSize;
	}

	public byte[] ReadWithCrc(out CRC crc)
	{
		crc = new CRC();
		var data = Read();
		crc.Add(data);

		return data;
	}

	private interface IInternalDecompressor
	{
		ushort DecodeP();
		ushort DecodeC();
		void DecodeStart(byte[] dictionaryText);
	}

	private class LZDecompressor : IInternalDecompressor
	{
		private const int Magic = 19;
		private int flagCount = 0;
		private ushort flag = 0;
		private int matchPosition = 0;
		private readonly Decompressor parent;

		public LZDecompressor(Decompressor parent)
		{
			this.parent = parent;
		}

		public ushort DecodeC()
		{
			if (flagCount == 0)
			{
				flagCount = 8;
				flag = (ushort)parent.GetC();
			}
			flagCount--;
			ushort c = (ushort)parent.GetC();
			if ((flag & 1) == 0)
			{
				matchPosition = c;
				c = (ushort)parent.GetC();
				matchPosition += (c & 0xf0) << 4;
				c &= 0x0f;
				c += 0x100;
			}
			flag >>= 1;
			return c;
		}

		public ushort DecodeP()
		{
			return (ushort)((parent.loc - matchPosition - Magic) & 0xfff);
		}

		public void DecodeStart(byte[] dictionaryText)
		{
			flagCount = 0;
			for (int i = 0; i < 256; i++)
			{
				Array.Fill(dictionaryText, (byte)i, i * 13 + 18, 13);
				dictionaryText[256 * 13 + 18 + i] = (byte)i;
				dictionaryText[256 * 13 + 256 + 18 + i] = (byte)(255 - i);
			}
			Array.Clear(dictionaryText, 256 * 13 + 512 + 18, 128);
			Array.Fill(dictionaryText, (byte)0x20, 256 * 13 + 512 + 128 + 18, 128 - 18);
		}
	}

	private class LHDecompressor : IInternalDecompressor
	{
		private const int MaxMatch = 256;
		private const int MaxDictBits = LZHuff7DictBits;
		private const int NP = MaxDictBits + 1;
		private const int NC = byte.MaxValue + MaxMatch + 2 - Threshold;
		private const int NT = 19; // USHRT_BIT + 3
		private const int TBIT = 5; // smallest integer such that (1 << TBIT) > * NT
		private const int PBIT = 5; // smallest integer such that (1 << PBIT) > * NP
		private const int CBIT = 9; // smallest integer such that (1 << CBIT) > * NC
		private const int NPT = 0x80;
		private readonly ushort[] left = new ushort[2 * NC - 1];
		private readonly ushort[] right = new ushort[2 * NC - 1];
		private readonly ushort[] c_table = new ushort[4096];
		private readonly ushort[] pt_table = new ushort[256];
		private readonly byte[] c_len = new byte[NC];
		private readonly byte[] pt_len = new byte[NPT];
		private int blockSize = 0;
		private readonly int pbit;
		private readonly int np;
		private readonly Decompressor parent;

		public LHDecompressor(Decompressor parent, int dictBits, int pbit)
		{
			this.parent = parent;
			this.pbit = pbit;
			np = dictBits + 1;
		}

		public ushort DecodeC()
		{
			if (blockSize == 0)
			{
				blockSize = parent.GetBits(16);
				ReadPtLength(NT, TBIT, 3);
				ReadCLength();
				ReadPtLength(np, pbit, -1);
			}

			blockSize--;

			ushort j = c_table[parent.PeekBits(12)];

			if (j < NC)
				parent.FillBuf(c_len[j]);
			else
			{
				parent.FillBuf(12);
				ushort mask = 1 << (16 - 1);
				do
				{
					if ((parent.bitBuf & mask) != 0)
						j = right[j];
					else
						j = left[j];
					mask >>= 1;
				} while (j >= NC && (mask != 0 || j != left[j]));
				parent.FillBuf(c_len[j] - 12);
			}

			return j;
		}

		public ushort DecodeP()
		{
			ushort j = pt_table[parent.PeekBits(8)];

			if (j < np)
				parent.FillBuf(pt_len[j]);
			else
			{
				parent.FillBuf(8);
				ushort mask = 1 << (16 - 1);
				do
				{
					if ((parent.bitBuf & mask) != 0)
						j = right[j];
					else
						j = left[j];
					mask >>= 1;
				} while (j >= np && (mask != 0 || j != left[j]));
				parent.FillBuf(pt_len[j] - 8);
			}

			if (j != 0)
				j = (ushort)((1 << (j - 1)) + parent.GetBits(j - 1));

			return j;
		}

		public void DecodeStart(byte[] dictionaryText)
		{
			// nothing needed
		}

		private void ReadPtLength(int nn, int nbit, int special)
		{
			ushort c;
			int n = parent.GetBits(nbit);
			if (n == 0)
			{
				c = parent.GetBits(nbit);
				for (int i = 0; i < nn; i++)
					pt_len[i] = 0;
				for (int i = 0; i < 256; i++)
					pt_table[i] = c;
			}
			else
			{
				int i = 0;
				while (i < Math.Min(n, NPT))
				{
					c = parent.PeekBits(3);
					if (c != 7)
						parent.FillBuf(3);
					else
					{
						ushort mask = 1 << (16 - 4);

						while ((mask & parent.bitBuf) != 0)
						{
							mask >>= 1;
							c++;
						}

						parent.FillBuf(c - 3);
					}

					pt_len[i++] = (byte)c;

					if (i == special)
					{
						c = parent.GetBits(2);

						while (--c >= 0 && i < NPT)
							pt_len[i++] = 0;
					}
				}

				while (i < nn)
					pt_len[i++] = 0;

				MakeTable(nn, pt_len, 8, pt_table);
			}
		}

		private void MakeTable(int n, byte[] bitLen, int tableBits, ushort[] table)
		{
			var count = new ushort[17]; // count of bitLen
			var weight = new ushort[17]; // 0x10000ul >> bitLen
			var start = new ushort[17]; // first code of bitLen

			int avail = n;

			for (int i = 1; i <= 16; i++)
			{
				count[i] = 0;
				weight[i] = 1 << (16 - 1);
			}

			for (int i = 0; i < n; i++)
			{
				if (bitLen[i] > 16)
					throw new InvalidDataException("Bad table");

				count[bitLen[i]]++;
			}

			int total = 0;

			for (int i = 1; i <= 16; i++)
			{
				start[i] = (ushort)total;
				total += weight[i] * count[i];
			}

			if ((total & 0xffff) != 0 || tableBits > 16)
				throw new InvalidDataException("Bad table");

			int m = 16 - tableBits;

			for (int i = 1; i <= tableBits; i++)
			{
				start[i] >>= m;
				weight[i] >>= m;
			}

			int j = start[tableBits + 1] >> m;
			ushort k = (ushort)Math.Min(1 << tableBits, 4096);

			if (j != 0)
			{
				for (int i = j; i < k; i++)
					table[i] = 0;
			}

			for (j = 0; j < n; j++)
			{
				k = bitLen[j];

				if (k == 0)
					continue;

				ushort l = (ushort)(start[k] + weight[k]);

				if (k <= tableBits)
				{
					l = Math.Min(l, (ushort)4096);

					for (int i = start[k]; i < l; i++)
						table[i] = (ushort)j;
				}
				else
				{
					int i = start[k];
					int tableIndex = i >> m;

					if (tableIndex > 4096)
						throw new InvalidDataException("Bad table");

					var dest = table;

					i <<= tableBits;
					int nn = k - tableBits;

					while (--nn >= 0)
					{
						if (dest[tableIndex] == 0)
						{
							right[avail] = left[avail] = 0;
							dest[tableIndex] = (ushort)avail++;
						}

						tableIndex = dest[tableIndex];

						if ((i & 0x8000) != 0)
						{
							dest = right;							
						}
						else
						{
							dest = left;
						}

						i <<= 1;
					}

					dest[tableIndex] = (ushort)j;
				}

				start[k] = l;
			}
		}

		private void ReadCLength()
		{
			int n = parent.GetBits(CBIT);
			ushort c;

			if (n == 0)
			{
				c = parent.GetBits(CBIT);

				for (int i = 0; i < NC; i++)
					c_len[i] = 0;
				for (int i = 0; i < 4096; i++)
					c_table[i] = c;
			}
			else
			{
				int i = 0;

				while (i < Math.Min(n, NC))
				{
					c = pt_table[parent.PeekBits(8)];

					if (c >= NT)
					{
						ushort mask = 1 << (16 - 9);

						do
						{
							if ((parent.bitBuf & mask) != 0)
								c = right[c];
							else
								c = left[c];

							mask >>= 1;
						} while (c >= NT && (mask != 0 || c != left[c]));
					}

					parent.FillBuf(pt_len[c]);

					if (c <= 2)
					{
						if (c == 0)
							c = 1;
						else if (c == 1)
							c = (ushort)(parent.GetBits(4) + 3);
						else
							c = (ushort)(parent.GetBits(CBIT) + 20);

						while (--c >= 0)
							c_len[i++] = 0;
					}
					else
						c_len[i++] = (byte)(c - 2);
				}

				while (i < NC)
					c_len[i++] = 0;

				MakeTable(NC, c_len, 12, c_table);
			}
		}
	}

	public byte[] Read()
	{
		if (method == CompressionMethod.None)
			return reader.ReadBytes((int)rawSize);

		int dictBits = method switch
		{
			CompressionMethod.LH5 => LZHuff5DictBits,
			CompressionMethod.LH6 => LZHuff6DictBits,
			CompressionMethod.LH7 => LZHuff7DictBits,
			CompressionMethod.LZ5 => LZArc5DictBits,
			_ => LZHuff5DictBits
		};
		int pbit = method switch
		{
			CompressionMethod.LH5 => 4,
			CompressionMethod.LH6 => 5,
			CompressionMethod.LH7 => 5,
			_ => 0,
		};
		int dictSize = 1 << dictBits;
		byte[] dtext = new byte[dictSize];
		IInternalDecompressor decompressor = method == CompressionMethod.LZ5
			? new LZDecompressor(this)
			: new LHDecompressor(this, dictBits, pbit);
		bitBuf = 0;
		subBitBuf = 0;
		bitCount = 0;
		FillBuf(2 * 8);
		List<byte> rawData = new List<byte>((int)rawSize);

		decompressor.DecodeStart(dtext);

		int dictSizeMask = dictSize - 1;
		int adjust = 256 - Threshold;
		int decodeCount = 0;
		loc = 0;

		while (decodeCount < rawSize)
		{
			ushort c = decompressor.DecodeC();

			if (c < 256) // literal
			{
				dtext[loc++] = (byte)c;

				if (loc == dictSize)
				{
					rawData.AddRange(dtext);
					loc = 0;
				}

				decodeCount++;
			}
			else // match
			{
				int matchLength = c - adjust;
				int matchOffset = decompressor.DecodeP() + 1;
				int matchPosition = (loc - matchOffset) & dictSizeMask;

				decodeCount += matchLength;

				for (int i = 0; i < matchLength; i++)
				{
					c = dtext[(matchPosition + i) & dictSizeMask];
					dtext[loc++] = (byte)c;

					if (loc == dictSize)
					{
						rawData.AddRange(dtext);
						loc = 0;
					}
				}
			}
		}

		if (loc != 0)
		{
			rawData.AddRange(dtext.Take(loc));
		}

		return rawData.ToArray();
	}

	private ushort PeekBits(int count)
	{
		return (ushort)(bitBuf >> (2 * 8 - count));
	}

	private ushort GetBits(int count)
	{
		ushort x = PeekBits(count);
		FillBuf(count);
		return x;
	}

	private int GetC()
	{
		if (reader.BaseStream.Position >= reader.BaseStream.Length)
			return EOF;

		return reader.ReadByte();
	}

	private void FillBuf(int n)
	{
		while (n > bitCount)
		{
			n -= bitCount;
			bitBuf = (ushort)((bitBuf << bitCount) + (subBitBuf >> (8 - bitCount)));
			if (compsize != 0)
			{
				compsize--;
				int c = GetC();
				if (c == EOF)
					throw new IndexOutOfRangeException("Read beyond end of file");
				subBitBuf = (byte)c;
			}
			else
			{
				subBitBuf = 0;
			}
			bitCount = 8;
		}
		bitCount -= n;
		bitBuf = (ushort)((bitBuf << n) + (subBitBuf >> (8 - n)));
		subBitBuf <<= n;
	}
}
