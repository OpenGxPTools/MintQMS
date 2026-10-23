using OpenQMS.Models;

namespace OpenQMS.Models.ViewModels
{
    public class ApproveDocumentViewModel
    {
        public int DocumentId { get; set; }
        public string InputEmail { get; set; } = string.Empty;
        public string InputPassword { get; set; } = string.Empty;
        public DateTime? ReviewDueDate { get; set; }
        public List<TrainingGroupViewModel> TrainingGroups { get; set; } = new();
    }

    public class TrainingGroupViewModel
    {
        public string Name { get; set; } = string.Empty;
        public TrainingType TrainingType { get; set; } = TrainingType.ReadAndAcknowledge;
        public string? TrainerId { get; set; }
        public List<string> SelectedRoles { get; set; } = new();
        public List<int> SelectedUserIds { get; set; } = new();
        public DateTime? DueDate { get; set; }
        public string? Description { get; set; }
    }
}
