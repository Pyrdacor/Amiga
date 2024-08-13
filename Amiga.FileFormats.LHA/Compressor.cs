namespace Amiga.FileFormats.LHA
{
    internal class Compressor
    {
        public static readonly Dictionary<CompressionMethod, string> SupportedCompressions = new()
        {
            { CompressionMethod.None, "-lh0-" },
            { CompressionMethod.LH5, "-lh5-" },
            { CompressionMethod.LH6, "-lh6-" },
            { CompressionMethod.LH7, "-lh7-" }
        };

        private const int MaxMatch = 256;
        private const int LZHuff5DictBits = 13;
        private const int LZHuff6DictBits = 15;
        private const int LZHuff7DictBits = 16;
        private const int MaxDictBits = LZHuff7DictBits;
        private const int MaxDictSize = 1 << MaxDictBits;
        private const int HashSize = 1 << 15;
        private const int TextSize = MaxDictSize * 2 + MaxMatch;
        private const int N1 = 286; // alphabet size
        private const int N2 = (2 * N1 - 1); // number of nodes in Huffman tree
        private const int ExtraBits = 8; // >= log2(MaxMatch-Threshold+258-N1), >=log2(225)
        private const int BufferBits = 16; // >= log2(BufferSize)
        private const int LengthField = 4;
        private const int Threshold = 3;
        private const int HashChainLimit = 0x100;
        private const int HashMask = HashSize - 1;
        private const int CharBits = 8;
        private const int BufferSize = 16 * 1024 * 2; // 65408
        private const int NP = MaxDictBits + 1;
        private const int NT = 18; // USHRT_BIT + 3
        private const int NC = byte.MaxValue + MaxMatch + 2 - Threshold;
        private const int NPT = 0x80;
        private const int PBIT = 5; // smallest integer such that (1 << PBIT) > * NP
        private const int TBIT = 5; // smallest integer such that (1 << TBIT) > * NT
        private const int CBIT = 9; // smallest integer such that (1 << CBIT) > * NC

        private struct MatchData
        {
            public int Length;
            public int Offset;
        }

        private struct Hash
        {
            public int Position;
            public bool TooLongFlag; // if true, matching candidate is too long
        }

        private static int InitHash(byte[] text, int position)
        {
            return ((((text[position] << 5) ^ text[position + 1]) << 5) ^ text[position + 2]) & HashMask;
        }

        private static int NextHash(int hash, byte[] text, int position)
        {
            return ((hash << 5) ^ text[position + 2]) & HashMask;
        }

        public CRC Compress(BinaryWriter writer, CompressionMethod method, byte[] rawData, out bool unpackable)
        {
            int ThrowUnsupported() => throw new NotSupportedException($"The compression method {Enum.GetName(method)} is not supported.");

            if (!SupportedCompressions.TryGetValue(method, out var methodName))
                ThrowUnsupported();

            var crc = new CRC();

            if (method == CompressionMethod.None)
            {
                crc.Add(rawData);
                writer.Write(rawData);
                unpackable = true;
                return crc;
            }

            int dictBits = method switch
            {
                CompressionMethod.LH5 => LZHuff5DictBits,
                CompressionMethod.LH6 => LZHuff6DictBits,
                CompressionMethod.LH7 => LZHuff7DictBits,
                _ => ThrowUnsupported()
            };
            int pbit = method switch
            {
                CompressionMethod.LH5 => 4,
                CompressionMethod.LH6 => 5,
                CompressionMethod.LH7 => 5,
                _ => ThrowUnsupported()
            };
            int np = dictBits + 1;
            int dictSize = 1 << dictBits;
            int textSize = dictSize * 2 + MaxMatch;
            MatchData match = new();
            MatchData last;
            match.Length = Threshold - 1;
            match.Offset = 0;
            byte[] text = new byte[textSize]; // This is basically the dictionary
            int dataPosition = 0;
            int remainder = ReadBytesAndUpdateCrc(dictSize, textSize - dictSize);
            if (match.Length > remainder)
                match.Length = remainder;
            int position = dictSize;
            int[] previousHashPosition = new int[dictSize];
            var hash = new Hash[HashSize];
            unpackable = false;
            ushort outputMask = 0;
            ushort outputPosition = 0;
            var buffer = new byte[BufferSize];
            ushort[] left = new ushort[2 * NC - 1];
            ushort[] right = new ushort[2 * NC - 1];
            ushort[] c_code = new ushort[NC];
            ushort[] pt_code = new ushort[NPT];
            ushort[] c_freq = new ushort[2 * NC - 1];
            ushort[] p_freq = new ushort[2 * NP - 1];
            ushort[] t_freq = new ushort[2 * NT - 1];
            byte[] c_len = new byte[NC];
            byte[] pt_len = new byte[NPT];
            int count = 0;
            int bitCount = 0;
            byte subBitBuffer = 0;
            int compressedSize = 0;

            for (int i = 0; i < HashSize; ++i)
            {
                hash[i] = new Hash();
            }

            int token = InitHash(text, position);
            InsertHash(token, position);

            while (remainder > 0 && !unpackable)
            {
                last = match;

                NextToken(ref token, ref position);
                SearchDict(token, position, last.Length - 1, ref match);
                InsertHash(token, position);

                if (match.Length > last.Length || last.Length < Threshold)
                {
                    // output a letter
                    Output(text[position - 1], 0);
                    count++;
                }
                else
                {
                    // output length and offset
                    Output((ushort)(last.Length + (256 - Threshold)), (ushort)((last.Offset - 1) & (dictSize - 1)));
                    count += last.Length;
                    --last.Length;

                    while (--last.Length > 0)
                    {
                        NextToken(ref token, ref position);
                        InsertHash(token, position);
                    }

                    NextToken(ref token, ref position);
                    SearchDict(token, position, Threshold - 1, ref match);
                    InsertHash(token, position);
                }
            }

            EncodeEnd();

            return crc;

            int ReadBytesAndUpdateCrc(int offset, int size)
            {
                int readSize = Math.Min(size, rawData.Length - dataPosition);
                byte[] readBuffer = new byte[readSize];
                Buffer.BlockCopy(rawData, dataPosition, readBuffer, 0, readSize);
                dataPosition += readSize;
                crc.Add(readBuffer);
                Buffer.BlockCopy(readBuffer, 0, text, offset, readSize);
                return readSize;
            }

            void InsertHash(int token, int position)
            {
                previousHashPosition[position & (dictSize - 1)] = hash[token].Position;
                hash[token].Position = position;
            }

            void NextToken(ref int token, ref int position)
            {
                remainder--;
                if (++position >= textSize - MaxMatch)
                    UpdateDict(ref position);
                token = NextHash(token, text, position);
            }

            void UpdateDict(ref int position)
            {
                Array.Copy(text, 0, text, dictSize, textSize - dictSize);
                int n = ReadBytesAndUpdateCrc(textSize - dictSize, dictSize);
                remainder += n;
                position -= dictSize;

                for (int i = 0; i < HashSize; ++i)
                {
                    int j = hash[i].Position;
                    hash[i].Position = j > dictSize ? j - dictSize : 0;
                    hash[i].TooLongFlag = false;
                }

                for (int i = 0; i < dictSize; ++i)
                {
                    int j = previousHashPosition[i];
                    previousHashPosition[i] = j > dictSize ? j - dictSize : 0;
                }
            }

            // Searches for the longest token matching the current token
            void SearchDict(int token, int position, int minMatchLength, ref MatchData match)
            {
                if (minMatchLength < Threshold - 1)
                    minMatchLength = Threshold - 1;

                int maxMatchLength = MaxMatch;
                match.Offset = 0;
                match.Length = minMatchLength;

                int offset = 0;
                int tok = token;

                while (hash[tok].TooLongFlag && offset < maxMatchLength - Threshold)
                {
                    // If matching position is too long, the search key is
                    // changed into following token from 'offset' (for speed).
                    ++offset;
                    tok = NextHash(tok, text, position + offset);
                }

                if (offset == maxMatchLength - Threshold)
                {
                    offset = 0;
                    tok = token;
                }

                SearchDictInternal(tok, position, offset, maxMatchLength, ref match);

                if (offset > 0 && match.Length < offset + 3)
                {
                    // re-search
                    SearchDictInternal(token, position, 0, offset + 2, ref match);
                }

                if (match.Length > remainder)
                    match.Length = remainder;
            }

            void SearchDictInternal(int token, int position, int offset, int maxMatchLength, ref MatchData match)
            {
                int chain = 0;
                int scanPosition = hash[token].Position;
                int scanBegin = scanPosition - offset;
                int scanEnd = position - dictSize;
                int length = 0;

                while (scanBegin > scanEnd)
                {
                    chain++;

                    if (text[scanBegin + match.Length] == text[position + match.Length])
                    {
                        unsafe
                        {
                            fixed (byte* aPtr = &text[scanBegin])
                            fixed (byte* bPtr = &text[position])
                            {
                                byte* a = aPtr;
                                byte* b = bPtr;

                                for (length = 0; length < maxMatchLength && *a++ == *b++; length++)
                                    ; // this empty loop just counts the match length
                            }
                        }

                        if (length > match.Length)
                        {
                            match.Offset = position - scanBegin;
                            match.Length = length;

                            if (match.Length == maxMatchLength)
                                break;
                        }
                    }

                    scanPosition = previousHashPosition[scanPosition & (dictSize - 1)];
                    scanBegin = scanPosition - offset;
                }

                if (chain >= HashChainLimit)
                    hash[token].TooLongFlag = true;
            }

            void EncodeEnd()
            {
                if (!unpackable)
                {
                    SendBlock();
                    PutBits(CharBits - 1, 0); // flush remaining bits
                }
            }

            void Output(ushort c, ushort p)
            {
                int cpos = 0;
                outputMask >>= 1;

                if (outputMask == 0)
                {
                    outputMask = 1 << (CharBits - 1);

                    if (outputPosition >= BufferSize - 3 * CharBits)
                    {
                        SendBlock();

                        if (unpackable)
                            return;

                        outputPosition = 0;
                    }

                    cpos = outputPosition++;
                    buffer[cpos] = 0;
                }

                buffer[outputPosition++] = (byte)c;
                c_freq[c]++;

                if (c >= (1 << CharBits))
                {
                    buffer[cpos] |= (byte)outputMask;
                    buffer[outputPosition++] = (byte)(p >> CharBits);
                    buffer[outputPosition++] = (byte)(p & 0xff);
                    c = 0;

                    while (p != 0)
                    {
                        p >>= 1;
                        c++;
                    }

                    p_freq[c]++;
                }
            }

            void SendBlock()
            {
                ushort root = (ushort)MakeTree(NC, c_freq, c_len, c_code);
                ushort size = c_freq[root];
                PutBits(16, size);

                if (root >= NC)
                {
                    CountTreeFreq();
                    root = (ushort)MakeTree(NT, t_freq, pt_len, pt_code);

                    if (root >= NT)
                        WritePreTreeLength(NT, TBIT, 3);
                    else
                    {
                        PutBits(TBIT, 0);
                        PutBits(TBIT, root);
                    }
                    WriteCodeLength();
                }
                else
                {
                    PutBits(TBIT, 0);
                    PutBits(TBIT, 0);
                    PutBits(CBIT, 0);
                    PutBits(CBIT, root);
                }

                root = (ushort)MakeTree(np, p_freq, pt_len, pt_code);

                if (root >= np)
                {
                    WritePreTreeLength((short)np, (short)pbit, -1);
                }
                else
                {
                    PutBits(pbit, 0);
                    PutBits(pbit, root);
                }

                ushort position = 0;
                byte flags = 0;

                for (int i = 0; i < size; i++)
                {
                    if (i % CharBits == 0)
                        flags = buffer[position++];
                    else
                        flags <<= 1;

                    if ((flags & (1 << (CharBits - 1))) != 0)
                    {
                        // Match
                        EncodeWithTree(buffer[position++] + (1 << CharBits));
                        int k = buffer[position++] << CharBits;
                        k += buffer[position++];
                        EncodeWithPreTree(k);
                    }
                    else
                    {
                        // Literal
                        EncodeWithTree(buffer[position++]);
                    }

                    if (unpackable)
                        return;
                }
                for (int i = 0; i < NC; i++)
                    c_freq[i] = 0;
                for (int i = 0; i < np; i++)
                    p_freq[i] = 0;
            }

            void EncodeWithTree(int value)
            {
                PutCode(c_len[value], c_code[value]);
            }

            void EncodeWithPreTree(int value)
            {
                int c = 0;
                int v = value;

                while (v != 0)
                {
                    v >>= 1;
                    c++;
                }

                PutCode(pt_len[c], pt_code[c]);

                if (c > 1)
                    PutBits(c - 1, (uint)value);
            }

            void WriteCodeLength()
            {
                short n = NC;

                while (n > 0 && c_len[n - 1] == 0)
                    n--;

                PutBits(CBIT, (uint)n);

                int i = 0;

                while (i < n)
                {
                    byte k = c_len[i];

                    if (k == 0)
                    {
                        short count = 1;

                        while (i < n && c_len[i] == 0)
                        {
                            i++;
                            count++;
                        }

                        if (count <= 2)
                        {
                            for (k = 0; k < count; k++)
                                PutCode(pt_len[0], pt_code[0]);
                        }
                        else if (count <= 18)
                        {
                            PutCode(pt_len[1], pt_code[1]);
                            PutBits(4, (uint)count - 3);
                        }
                        else if (count == 19)
                        {
                            PutCode(pt_len[0], pt_code[0]);
                            PutCode(pt_len[1], pt_code[1]);
                            PutBits(4, 15);
                        }
                        else
                        {
                            PutCode(pt_len[2], pt_code[2]);
                            PutBits(CBIT, (uint)count - 20);
                        }
                    }
                    else
                    {
                        PutCode(pt_len[k + 2], pt_code[k + 2]);
                    }
                }
            }

            void WritePreTreeLength(short n, short nbit, short special)
            {
                while (n > 0 && pt_len[n - 1] == 0)
                    n--;

                PutBits(nbit, (ushort)n);
                int i = 0;
                
                while (i < n)
                {
                    ushort k = pt_len[i++];

                    if (k <= 6)
                        PutBits(3, k);
                    else
                        PutBits(k - 3, ushort.MaxValue << 1); // k=7 -> 1110  k=8 -> 11110  k=9 -> 111110 ...

                    if (i == special)
                    {
                        while (i < 6 && pt_len[i] == 0)
                            i++;
                        PutBits(2, (uint)i - 3);
                    }
                }
            }

            void CountTreeFreq()
            {
                short n = NC;

                while (n > 0 && c_len[n - 1] == 0)
                    n--;

                short i = 0;

                while (i < n)
                {
                    short k = c_len[i++];

                    if (k == 0)
                    {
                        short count = 1;

                        while (i < n && c_len[i] == 0)
                        {
                            i++;
                            count++;
                        }

                        if (count <= 2)
                            t_freq[0] += (ushort)count;
                        else if (count <= 18)
                            t_freq[1]++;
                        else if (count == 19)
                        {
                            t_freq[0]++;
                            t_freq[1]++;
                        }
                        else
                            t_freq[2]++;
                    }
                    else
                    {
                        t_freq[k + 2]++;
                    }
                }
            }

            void PutBits(int bits, uint value)
            {
                value <<= 16 - bits;
                PutCode(bits, value);
            }

            void PutCode(int bits, uint code)
            {
                while (bits >= bitCount)
                {
                    bits -= bitCount;
                    subBitBuffer += (byte)(code >> (16 - bitCount));
                    code <<= bitCount;

                    if (compressedSize < rawData.Length)
                    {
                        writer.Write(subBitBuffer);
                        compressedSize++;
                    }
                    else
                    {
                        unpackable = true;
                    }

                    subBitBuffer = 0;
                    bitCount = CharBits;
                }

                subBitBuffer += (byte)(code >> (16 - bitCount));
                bitCount -= bits;
            }

            short MakeTree(int nchar, ushort[] freq, byte[] bitLength, ushort[] code)
            {
                short[] heap = new short[NC + 1];
                int heapSize = 0;
                short avail = (short)nchar;
                heap[1] = 0;
                short i, j, root;

                for (i = 0; i < nchar; i++)
                {
                    bitLength[i] = 0;

                    if (freq[i] != 0)
                        heap[++heapSize] = i;
                }
                if (heapSize < 2)
                {
                    code[heap[1]] = 0;
                    return heap[1];
                }

                // Make priority queue
                for (i = (short)(heapSize / 2); i >= 1; i--)
                    DownHeap(i, heap, heapSize, freq);

                // Make huffman tree
                unsafe
                {
                    fixed (ushort* c = code)
                    {
                        ushort* sort = c;

                        do // while queue has at least two entries
                        {
                            i = heap[1]; // take out least-freq entry

                            if (i < nchar)
                                *sort++ = (ushort)i;

                            heap[1] = heap[heapSize--];
                            DownHeap(1, heap, heapSize, freq);
                            j = heap[1]; // next least-freq entry

                            if (j < nchar)
                                *sort++ = (ushort)j;

                            root = avail++; // generate new node
                            freq[root] = (ushort)(freq[i] + freq[j]);
                            heap[1] = root;
                            DownHeap(1, heap, heapSize, freq); // put into queue
                            left[root] = (ushort)i;
                            right[root] = (ushort)j;

                        } while (heapSize > 1);
                    }
                }

                ushort[] leafNum = new ushort[17];

                // Make leafNum
                CountLeaf(root, nchar, leafNum, 0);

                // Make bitLength
                MakeLength(nchar, bitLength, code, leafNum);

                // Make code table
                MakeCode(nchar, bitLength, code, leafNum);

                return root;
            }

            // priority queue; send i-th entry down heap
            void DownHeap(short i, short[] heap, int heapSize, ushort[] freq)
            {
                short j, k = heap[i];

                while ((j = (short)(2 * i)) <= heapSize)
                {
                    if (j < heapSize && freq[heap[j]] > freq[heap[j + 1]])
                        j++;
                    if (freq[k] <= freq[heap[j]])
                        break;
                    heap[i] = heap[j];
                    i = j;
                }

                heap[i] = k;
            }

            void MakeLength(int nchar, byte[] bitLength, ushort[] sort, ushort[] leafNum)
            {
                int k, s = 0;
                uint c = 0;

                for (int i = 16; i > 0; i--)
                {
                    c += (uint)(leafNum[i] << (16 - i));
                }

                c &= 0xffff;

                // Adjust length
                if (c != 0)
                {
                    leafNum[16] = (ushort)(leafNum[16] - c); // always leafNum[16] > c

                    do
                    {
                        for (int i = 15; i > 0; i--)
                        {
                            if (leafNum[i] != 0)
                            {
                                leafNum[i]--;
                                leafNum[i + 1] += 2;
                                break;
                            }
                        }
                    } while (--c != 0);
                }

                // Make length
                for (int i = 16; i > 0; i--)
                {
                    k = leafNum[i];

                    while (k > 0)
                    {
                        bitLength[sort[s++]] = (byte)i;
                        k--;
                    }
                }
            }

            void CountLeaf(int node, int nchar, ushort[] leafNum, int depth)
            {
                if (node < nchar)
                    leafNum[depth < 16 ? depth : 16]++;
                else
                {
                    CountLeaf(left[node], nchar, leafNum, depth + 1);
                    CountLeaf(right[node], nchar, leafNum, depth + 1);
                }
            }

            void MakeCode(int nchar, byte[] bitLength, ushort[] code, ushort[] leafNum)
            {
                ushort[] weight = new ushort[17];
                ushort[] start = new ushort[17];
                ushort total = 0;

                for (int i = 1; i <= 16; i++)
                {
                    start[i] = total;
                    weight[i] = (ushort)(1 << (16 - i));
                    total += (ushort)(weight[i] * leafNum[i]);
                }

                for (int c = 0; c < nchar; c++)
                {
                    int i = bitLength[c];
                    code[c] = start[i];
                    start[i] += weight[i];
                }
            }
        }
    }
}
