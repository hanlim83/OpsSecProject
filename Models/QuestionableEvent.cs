using System;
using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public enum QuestionableEventStatus
    {
        PendingReview,UserAccepted,UserRejected,AdminAccepted,AdminRejected,LockedAccepted, LockedRejected
    }
    public class QuestionableEvent
    {
        public int ID { get; set; }
        [Required]
        public string FullEventData { get; set; }
        [Required]
        public string UserField { get; set; }
        [Required]
        public string IPAddressField { get; set; }
        [Required]
        public DateTime EventTimestamp { get; set; }
        [Required]
        public int LinkedAlertTriggerID { get; set; }
        [Required]
        public virtual Trigger LinkedAlertTrigger { get; set; }
        [Required]
        public QuestionableEventStatus status { get; set; }
        public DateTime UpdatedTimestamp { get; set; }
        public int ReviewUserID { get; set; }
        public int AdministratorID { get; set; }
    }
}