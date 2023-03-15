using System.Text;

namespace Amiga.FileFormats.ADF
{
    internal class DataWriter
    {
        private int position = 0;
        private readonly List<byte> data = new();

        public int Size => data.Count;
        public int Position
        {
            get => position;
            set
            {
                if (value < 0 || value > Size)
                    throw new IndexOutOfRangeException("The given index was out of range.");

                position = value;
            }
        }

        public DataWriter(byte[] data)
        {
            this.data = new(data);
            Position = Size;
        }

        public DataWriter()
        {

        }

        public void WriteByte(byte value)
        {
            if (Position == Size)
            {
                data.Add(value);
                ++Position;
            }
            else
                data[Position++] = value;
        }

        public void WriteWord(ushort value)
        {
            WriteByte((byte)(value >> 8));
            WriteByte((byte)(value & 0xff));
        }

        public void WriteDword(uint value)
        {
            WriteWord((ushort)(value >> 16));
            WriteWord((ushort)(value & 0xffff));
        }

        public void WriteBytes(byte[] bytes)
        {
            if (Position == Size)
            {
                data.AddRange(bytes);
                Position = Size;
            }
            else
            {
                for (int i = 0; i < bytes.Length; ++i)
                    WriteByte(bytes[i]);
            }
        }

        public void WriteBytes(byte[] bytes, int offset, int count)
        {
            WriteBytes(new Span<byte>(bytes, offset, count).ToArray());
        }

        public void WriteChars(char[] chars)
        {
            WriteBytes(Encoding.ASCII.GetBytes(chars));
        }

        public void CopyTo(byte[] destination, int destinationOffset, int count)
        {
            Array.Copy(data.ToArray(), 0, destination, destinationOffset, count);
        }

        public byte[] ToArray() => data.ToArray();
    }
}
