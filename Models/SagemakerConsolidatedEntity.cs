using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public enum SagemakerStatus
    {
        Untrained, Training, Tuning, Deploying, Ready
    }
    public class SagemakerConsolidatedEntity
    {
        public int ID { get; set; }
        public SagemakerStatus SagemakerStatus { get; set; }
        public string SagemakerTrainingARN { get; set; }
        public string SagemakerEndpointConfigurationARN { get; set; }
        public string SagemakerEndpointDeploymentARN { get; set; }
        public string SagemakerHyperParameterTurningARN { get; set; }
        public string SagemakerBatchTransformARN { get; set; }
        public string SagemakerEndpoint { get; set; }
        [Required]
        public int LinkedLogInputID { get; set; }
        [Required]
        public virtual LogInput LinkedLogInput { get; set; }
    }
}
