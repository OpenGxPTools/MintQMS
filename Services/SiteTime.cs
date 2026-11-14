namespace OpenQMS
{
    /// <summary>
    /// Site time zone for display. Timestamps are stored in UTC (DateTime.UtcNow); everything shown
    /// to users — screens, exports, PDFs, e-mails — is converted to the site zone configured in
    /// System Settings (default UTC until the administrator chooses the site zone). "Today" for due/overdue logic is the site's today.
    /// </summary>
    public static class SiteTime
    {
        public const string DefaultZoneId = "UTC";

        /// <summary>The zone MintQMS used before the site zone could be any zone in the world. An existing
        /// installation that never saved its System Settings keeps it on upgrade.</summary>
        public const string LegacyDefaultZoneId = "Australia/Brisbane";

        private static readonly string[] Regions =
            { "Africa", "America", "Antarctica", "Arctic", "Asia", "Atlantic", "Australia", "Europe", "Indian", "Pacific" };

        private static readonly Lazy<IReadOnlyList<(string Id, string Group, string Label)>> _choices = new(BuildChoices);

        /// <summary>Every time zone in the world offered in System Settings (IANA ids — the server runs on Linux),
        /// grouped by continent, labelled "City (UTC±hh:mm)" with the offset in force today.</summary>
        public static IReadOnlyList<(string Id, string Group, string Label)> Choices => _choices.Value;

        private static IReadOnlyList<(string Id, string Group, string Label)> BuildChoices()
        {
            var now = DateTime.UtcNow;
            static string Offset(TimeSpan o) => "UTC" + (o < TimeSpan.Zero ? "-" : "+") + o.ToString(@"hh\:mm");
            var zones = TimeZoneInfo.GetSystemTimeZones();
            var list = zones
                .Where(z => z.Id.Contains('/') && Regions.Contains(z.Id[..z.Id.IndexOf('/')]))
                .Select(z => (Id: z.Id,
                              Group: z.Id[..z.Id.IndexOf('/')],
                              Label: z.Id[(z.Id.IndexOf('/') + 1)..].Replace('_', ' ').Replace("/", " / ")
                                     + " (" + Offset(z.GetUtcOffset(now)) + ")"))
                .OrderBy(z => z.Group, StringComparer.Ordinal).ThenBy(z => z.Label, StringComparer.Ordinal)
                .ToList();
            if (list.Count == 0)   // a server without IANA ids (Windows): offer its own zones
                list = zones.Select(z => (z.Id, Group: "", Label: z.DisplayName)).ToList();
            list.Insert(0, ("UTC", "", "UTC"));
            return list;
        }

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
            // Windows fallback for the former default zone
            if (id == LegacyDefaultZoneId)
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
