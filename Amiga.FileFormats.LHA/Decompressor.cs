using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amiga.FileFormats.LHA
{
    internal class Decompressor
    {
        public static readonly Dictionary<string, CompressionMethod> SupportedCompressions = new()
        {
            { "-lh0-", CompressionMethod.None },
            { "-lh5-", CompressionMethod.LH5 },
            { "-lh6-", CompressionMethod.LH6 },
            { "-lh7-", CompressionMethod.LH7 }
        };

        private readonly CRC crc = new();
        private readonly BinaryReader reader;

        public Decompressor(BinaryReader reader, CompressionMethod method, uint rawSize)
        {
            this.reader = reader;
        }

        public byte[] Read()
        {

        }
    }
}
