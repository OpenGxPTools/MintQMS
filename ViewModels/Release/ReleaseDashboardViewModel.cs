using OpenQMS.Models.Release;

namespace OpenQMS.ViewModels.Release
{
    public class ReleaseDashboardViewModel
    {
        public int TotalRecordsThisYear { get; set; }
        public int PendingAPDecision { get; set; }
        public int ReleasedYTD { get; set; }
        public int OnHold { get; set; }
        public int Rejected { get; set; }

        public Dictionary<string, int> ByType { get; set; } = new();
        public Dictionary<string, int> ByStatus { get; set; } = new();
        public List<MonthlyReleaseData> MonthlyTrend { get; set; } = new();
        public List<ReleaseRecord> RecentRecords { get; set; } = new();
    }

    public class MonthlyReleaseData
    {
        public string MonthLabel { get; set; } = string.Empty;
        public int RFS { get; set; }
        public int Quarantine { get; set; }
        public int RFFP { get; set; }
        public int ReleaseToCustomer { get; set; }
        public int QualityOversight { get; set; }
    }
}
