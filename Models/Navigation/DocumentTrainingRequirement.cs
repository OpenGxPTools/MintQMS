namespace OpenQMS.Models.Navigation
{
    public class DocumentTrainingRequirement
    {
        public int Id { get; set; }
        public int DocumentId { get; set; }
        public AppDocument Document { get; set; } = null!;
        public decimal DocumentVersion { get; set; }
        public int TrainingId { get; set; }
        public Training Training { get; set; } = null!;
        public DateTime CreatedOn { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
    }
}
