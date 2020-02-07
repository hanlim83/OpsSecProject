using System;
using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public enum AlertType
    {
        InputIngestSuccess, MetricExceeded, InputIngestPending, SageMakerTrained, SageMakerDeployed,SageMakerPredictionTriggered, SageMakerBatchTransformCompleted, MajorInformationChange
    }
    public enum ExternalNotificationType
    {
        EMAIL,SMS,NONE
    }
    public class Alert
    {
        public int ID { get; set; }
        [Required]
        public string Message { get; set; }
        public int LinkedObjectID { get; set; }
        [Required]
        public AlertType AlertType { get; set; }
        [Required]
        public DateTime TimeStamp { get; set; }
        [Required]
        public ExternalNotificationType ExternalNotificationType { get; set; }
        [Required]
        public bool Read { get; set; }
        [Required]
        public int LinkedUserID { get; set; }
        [Required]
        public virtual User LinkedUser { get; set; }
    }
}
