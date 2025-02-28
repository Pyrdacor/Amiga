using System.Text;

namespace Amiga.FileFormats.ADF;

internal class DataReader
{
    private readonly byte[] data;

    public int Size => data.Length;
    public int Position { get; set; } = 0;

    public DataReader(byte[] data)
    {
        this.data = data;
    }

    public uint PeekDword()
    {
        uint dword = data[Position];
        dword <<= 8;
        dword |= data[Position + 1];
        dword <<= 8;
        dword |= data[Position + 2];
        dword <<= 8;
        dword |= data[Position + 3];
        return dword;
    }

    public byte ReadByte() => data[Position++];

    public ushort ReadWord()
    {
        ushort word = ReadByte();
        word <<= 8;
        word |= ReadByte();
        return word;
    }

    public uint ReadDword()
    {
        uint dword = ReadWord();
        dword <<= 16;
        dword |= ReadWord();
        return dword;
    }

    public byte[] ReadBytes(int count)
    {
        var span = new Span<byte>(data, Position, count);
        Position += count;
        return span.ToArray();
    }

    public byte[] ReadToEnd() => ReadBytes(Size - Position);

    public char[] ReadChars(int count)
    {
        return Encoding.ASCII.GetChars(ReadBytes(count));
    }
}
