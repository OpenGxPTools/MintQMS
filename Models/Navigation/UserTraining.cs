/*
This file is part of the OpenQMS.net project (https://github.com/C-realize/OpenQMS).
Copyright (C) 2022-2024  C-realize IT Services SRL (https://www.c-realize.com)

This program is offered under a commercial and under the AGPL license.
For commercial licensing, contact us at https://www.c-realize.com/contact.  For AGPL licensing, see below.

AGPL:
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see http://www.gnu.org/licenses/.
*/

using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models.Navigation
{
    public class UserTraining
    {
        public int Id { get; set; }
        public int TraineeId { get; set; }
        public int TrainingId { get; set; }
        public AppUser Trainee { get; set; }
        public Training Training { get; set; }

        public UserTrainingStatus Status { get; set; } = UserTrainingStatus.Scheduled;

        [Display(Name = "Acknowledged On")]
        public DateTime? ReadAcknowledgedOn { get; set; }
        public string? ReadAcknowledgedSignature { get; set; }

        [Display(Name = "Competency Verified On")]
        public DateTime? CompetencyVerifiedOn { get; set; }
        [Display(Name = "Verified By")]
        public string? CompetencyVerifiedBy { get; set; }
        public string? CompetencyVerifiedSignature { get; set; }
        [Display(Name = "Competency Notes")]
        public string? CompetencyNotes { get; set; }

        public List<TrainingAttachment>? Attachments { get; set; }
    }

    public enum UserTrainingStatus
    {
        Scheduled,
        InProgress,
        [Display(Name = "Pending Trainer Confirmation")]
        PendingTrainerConfirmation,
        Completed,
        Failed,
        Cancelled
    }
}
