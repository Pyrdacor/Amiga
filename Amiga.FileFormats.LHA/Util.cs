namespace Amiga.FileFormats.LHA;

internal static class Util
{
    public static ushort ReadLEWord(BinaryReader reader)
    {
        uint value = reader.ReadByte();
        value |= ((uint)reader.ReadByte()) << 8;

        return (ushort)value;
    }

    public static ushort ReadLEWord(byte[] data, int offset)
    {
        if (BitConverter.IsLittleEndian)
            return BitConverter.ToUInt16(data, offset);

        uint value = data[offset];
        value |= ((uint)data[offset + 1]) << 8;

        return (ushort)value;
    }

    public static uint ReadLELong(byte[] data, int offset)
    {
        if (BitConverter.IsLittleEndian)
            return BitConverter.ToUInt32(data, offset);

        uint value = data[offset];
        value |= ((uint)data[offset + 1]) << 8;
        value |= ((uint)data[offset + 1]) << 16;
        value |= ((uint)data[offset + 1]) << 24;

        return value;
    }

    public static DateTime ReadUnixDateTime(byte[] data, int offset)
    {
	        long epochTicks = new DateTime(1970, 1, 1).Ticks;
			long elapsedTicks = ReadLELong(data, offset) * TimeSpan.TicksPerSecond;
        return new DateTime(epochTicks + elapsedTicks, DateTimeKind.Utc);
    }

    public static uint GetUnixDateTime(DateTime dateTime)
    {
			long epochTicks = new DateTime(1970, 1, 1).Ticks;
			long elapsedTicks = dateTime.Ticks - epochTicks;
			return (uint)(elapsedTicks / TimeSpan.TicksPerSecond);
		}

    public static DateTime? ReadGenericTimestamp(byte[] data, int offset)
    {
        /* Generic timestamp format
             31 30 29 28 27 26 25 24 23 22 21 20 19 18 17 16
            |<------ year ------>|<- month ->|<--- day ---->|
             15 14 13 12 11 10  9  8  7  6  5  4  3  2  1  0
            |<--- hour --->|<---- minute --->|<- second/2 ->|
        */

        uint value = ReadLELong(data, offset);

        if (value == 0) // no date given
            return null;

        int year = 1980 + (int)(value >> 25); // upper 7 bits
        int month = (int)(value >> 21) & 0xf; // next 4 bits
        int day = (int)(value >> 16) & 0x1f; // next 5 bits
        int hour = (int)(value >> 11) & 0x1f; // next 5 bits
        int minute = (int)(value >> 5) & 0x3f; // next 6 bits
        int second = 2 * (int)(value & 0x1f); // last 5 bits

        return new DateTime(year, month, day, hour, minute, second);
    }

    public static uint GetGenericTimestamp(DateTime dateTime)
    {
        uint year = (uint)dateTime.Year;
        if (year < 1980)
            throw new ArgumentOutOfRangeException("Dates before 1980 are not supported.");
        if (year > 2107)
            throw new ArgumentOutOfRangeException("Dates after 2107 are not supported.");
        return ((year - 1980) << 25) | ((uint)dateTime.Month << 21) | ((uint)dateTime.Day << 16) | ((uint)dateTime.Hour << 11) | ((uint)dateTime.Minute << 5) | ((uint)dateTime.Second >> 1);
    }
}
