using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public enum SagemakerStatus
    {
        Untrained, Training, Trained, Tuning, Deploying, Reversing,Ready
    }

    public enum SagemakerAlgorithm
    {
        IP_Insights,Random_Cut_Forest
    }
    public class SagemakerConsolidatedEntity
    {
        public int ID { get; set; }
        [Required]
        public SagemakerAlgorithm SagemakerAlgorithm { get; set; }
        public SagemakerStatus SagemakerStatus { get; set; }
        public string ModelName { get; set; }
        public string TrainingJobName { get; set; }
        public string TrainingJobARN { get; set; }
        public string EndpointConfigurationName { get; set; }
        public string EndpointConfigurationARN { get; set; }
        public string EndpointName { get; set; }
        public string EndpointJobARN { get; set; }
        public string HyperParameterTurningJobName { get; set; }
        public string HyperParameterTurningJobARN { get; set; }
        public string BatchTransformJobName { get; set; }
        public string BatchTransformJobARN { get; set; }
        [Required]
        public int LinkedLogInputID { get; set; }
        [Required]
        public virtual LogInput LinkedLogInput { get; set; }
    }
}
