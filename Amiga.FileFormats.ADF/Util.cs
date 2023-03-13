namespace Amiga.FileFormats.ADF
{
    internal static class Util
    {
        private static readonly int[,] DaysPerMonth = new int[2,12]
        {
            { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 },
            { 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 }
        };

        public static void WriteDateTime(DataWriter writer, DateTime dateTime)
        {
            uint days = 0;

            for (int year = 1978; year < dateTime.Year; ++year)
            {
                if (year % 4 == 0 && (year % 400 == 0 || year % 100 != 0))
                    days += 366;
                else
                    days += 365;
            }

            if (dateTime.Month != 0)
            {
                int index;

                if (dateTime.Year % 4 == 0 && (dateTime.Year % 400 == 0 || dateTime.Year % 100 != 0))
                    index = 1;
                else
                    index = 0;

                for (int m = 0; m < dateTime.Month; ++m)
                {
                    days = (uint)(days + DaysPerMonth[index, m]);
                }
            }

            days = (uint)(days + dateTime.Day);

            writer.WriteDword(days);
            writer.WriteDword((uint)(dateTime.Hour * 60 + dateTime.Minute));
            writer.WriteDword((uint)(dateTime.Second * 50 + dateTime.Millisecond / 20));
        }

        public static DateTime ReadDateTime(DataReader reader)
        {
            uint days = reader.ReadDword();
            uint minutes = reader.ReadDword();
            uint ticks = reader.ReadDword();

            if (minutes > 24 * 60 || ticks > 50 * 60)
                throw new InvalidDataException("Invalid date time data.");

            int year = 1978;
            int daysPerYear = 365;

            while (days > daysPerYear)
            {
                ++year;
                days = (uint)(days - daysPerYear);

                if (year % 4 == 0 && (year % 400 == 0 || year % 100 != 0))
                    daysPerYear = 366;
                else
                    daysPerYear = 365;
            }

            int index = daysPerYear - 365;
            int month = 0;
            int day = 0;

            for (int m = 0; m < 12; ++m)
            {
                int daysPerMonth = DaysPerMonth[index, m];

                if (days < daysPerMonth)
                {
                    month = m;
                    day = (int)days;
                    break;
                }

                days = (uint)(days - daysPerMonth);
            }

            return new DateTime(year, month, day, (int)minutes / 60, (int)minutes % 60, (int)ticks / 50, ((int)ticks % 50) * 20);
        }
    }
}
