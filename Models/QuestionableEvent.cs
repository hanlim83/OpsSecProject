using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace OpsSecProject.Models
{
    public enum QuestionableEventStatus
    {
        PendingReview,UserAccepted,UserRejected,AdminAccepted,AdminRejected,Locked
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