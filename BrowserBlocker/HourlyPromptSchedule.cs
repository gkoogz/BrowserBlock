using System;

namespace BrowserBlocker
{
    public static class HourlyPromptSchedule
    {
        public static DateTime GetHourKey(DateTime localTime)
        {
            return new DateTime(
                localTime.Year,
                localTime.Month,
                localTime.Day,
                localTime.Hour,
                0,
                0,
                localTime.Kind);
        }

        public static bool ShouldShow(DateTime localTime, DateTime lastPromptHour, bool isBlocked)
        {
            return !isBlocked &&
                localTime.Minute == 0 &&
                GetHourKey(localTime) != lastPromptHour;
        }

        public static bool ShouldShowBlockExpiration(
            TimeSpan remaining,
            bool isBlocked,
            bool promptAlreadyShown)
        {
            return isBlocked &&
                !promptAlreadyShown &&
                remaining > TimeSpan.Zero &&
                remaining <= TimeSpan.FromSeconds(59);
        }
    }
}
