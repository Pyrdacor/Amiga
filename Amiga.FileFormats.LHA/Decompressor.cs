using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amiga.FileFormats.LHA
{
    internal class Decompressor
    {
        public enum Method
        {
            None,
            LH5,
            LH6,
            LH7
        }

        public static readonly Dictionary<string, Method> SupportedCompressions = new()
        {
            { "-lh0-", Method.None },
            { "-lh5-", Method.LH5 },
            { "-lh6-", Method.LH6 },
            { "-lh7-", Method.LH7 }
        };

        private readonly CRC crc = new();
        private readonly BinaryReader reader;

        public Decompressor(BinaryReader reader, Method method, uint rawSize)
        {
            this.reader = reader;
        }

        public byte[] Read()
        {

        }
    }
}
