namespace DUANCHAMCONG.Helpers
{
    public static class TimeHelper
    {
        public static DateTime VietnamNow()
        {
            return DateTime.UtcNow.AddHours(7);
        }

        public static DateTime EnsureUtc(DateTime dateTime)
        {
            return dateTime.Kind switch
            {
                DateTimeKind.Utc => dateTime,
                DateTimeKind.Local => dateTime.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            };
        }

        public static DateTime ToVietnamTime(DateTime utcTime)
        {
            return EnsureUtc(utcTime).AddHours(7);
        }

        public static DateTime VietnamToUtc(DateTime vnTime)
        {
            return DateTime.SpecifyKind(
                vnTime.AddHours(-7),
                DateTimeKind.Utc
            );
        }
    }
}