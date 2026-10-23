namespace OpenQMS
{
    /// <summary>
    /// Site time zone for display. Timestamps are stored in UTC (DateTime.UtcNow); everything shown
    /// to users — screens, exports, PDFs, e-mails — is converted to the site zone configured in
    /// System Settings (default Australia/Brisbane). "Today" for due/overdue logic is the site's today.
    /// </summary>
    public static class SiteTime
    {
        public const string DefaultZoneId = "Australia/Brisbane";

        /// <summary>Zones offered in System Settings (IANA ids — the server runs on Linux).</summary>
        public static readonly (string Id, string Label)[] Choices =
        {
            ("Australia/Brisbane", "Brisbane (AEST, UTC+10, no daylight saving)"),
            ("Australia/Sydney",   "Sydney / Canberra (AEST/AEDT)"),
            ("Australia/Melbourne","Melbourne (AEST/AEDT)"),
            ("Australia/Hobart",   "Hobart (AEST/AEDT)"),
            ("Australia/Adelaide", "Adelaide (ACST/ACDT)"),
            ("Australia/Darwin",   "Darwin (ACST, no daylight saving)"),
            ("Australia/Perth",    "Perth (AWST, no daylight saving)"),
            ("Pacific/Auckland",   "Auckland (NZST/NZDT)"),
            ("UTC",                "UTC"),
        };

        private static TimeZoneInfo _zone = Resolve(DefaultZoneId) ?? TimeZoneInfo.Utc;

        public static TimeZoneInfo Zone => _zone;

        /// <summary>Set the site zone (at startup and when System Settings are saved). Unknown ids are ignored.</summary>
        public static void Configure(string? zoneId)
        {
            var tz = Resolve(string.IsNullOrWhiteSpace(zoneId) ? DefaultZoneId : zoneId!);
            if (tz != null) _zone = tz;
        }

        public static bool IsValid(string? zoneId) => !string.IsNullOrWhiteSpace(zoneId) && Resolve(zoneId!) != null;

        private static TimeZoneInfo? Resolve(string id)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
            // Windows fallback for the default zone
            if (id == DefaultZoneId)
                try { return TimeZoneInfo.FindSystemTimeZoneById("E. Australia Standard Time"); } catch { }
            return null;
        }

        /// <summary>Current date/time at the site.</summary>
        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone);

        /// <summary>Today's date at the site (for due / overdue comparisons against calendar dates).</summary>
        public static DateTime Today => Now.Date;

        /// <summary>
        /// Convert a stored UTC timestamp to site time. Values read from the database come back with
        /// Kind=Unspecified and are UTC by convention; a Local value is converted from server time.
        /// </summary>
        public static DateTime ToSite(this DateTime value)
        {
            if (value == DateTime.MinValue || value == DateTime.MaxValue) return value;
            var utc = value.Kind switch
            {
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
            return TimeZoneInfo.ConvertTimeFromUtc(utc, _zone);
        }

        public static DateTime? ToSite(this DateTime? value) => value?.ToSite();

        /// <summary>Short zone label for column headers, e.g. "AEST" (or "UTC+10:00" for unmapped zones).</summary>
        public static string Label
        {
            get
            {
                bool dst = _zone.IsDaylightSavingTime(DateTime.UtcNow);
                return _zone.Id switch
                {
                    "Australia/Brisbane" or "E. Australia Standard Time" or "Australia/Lindeman" => "AEST",
                    "Australia/Sydney" or "Australia/Melbourne" or "Australia/Hobart" or "Australia/Canberra"
                        or "AUS Eastern Standard Time" => dst ? "AEDT" : "AEST",
                    "Australia/Adelaide" or "Australia/Broken_Hill" => dst ? "ACDT" : "ACST",
                    "Australia/Darwin" => "ACST",
                    "Australia/Perth" => "AWST",
                    "Pacific/Auckland" => dst ? "NZDT" : "NZST",
                    "UTC" or "Etc/UTC" => "UTC",
                    _ => "UTC" + (_zone.GetUtcOffset(DateTime.UtcNow) < TimeSpan.Zero ? "-" : "+")
                                 + _zone.GetUtcOffset(DateTime.UtcNow).ToString(@"hh\:mm")
                };
            }
        }
    }
}
