using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public enum SagemakerStatus
    {
        Untrained, Training, Trained, Tuning, Deploying, Reversing, Transforming, Ready, Error, None
    }
    public enum SagemakerErrorStage
    {
        Training, Tuning, Transforming, Deployment, None
    }
    public enum AlertTriggerType
    {
        CountByTimeStamp, CountAlone, IPInsights, RCF
    }
    public class Trigger
    {
        public int ID { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public AlertTriggerType AlertTriggerType { get; set; }
        [Required]
        public string CondtionalField { get; set; }
        [Required]
        public string CondtionType { get; set; }
        [Required]
        public string Condtion { get; set; }
        public string IPAddressField { get; set; }
        public string UserField { get; set; }
        public string CurrentInputDataKey { get; set; }
        public string[] DeprecatedInputDataKeys { get; set; }
        public string CurrentModelFileKey { get; set; }
        public string CheckpointKey { get; set; }
        public SagemakerStatus SagemakerStatus { get; set; }
        public SagemakerErrorStage SagemakerErrorStage { get; set; }
        public string CurrentModelName { get; set; }
        public string[] DeprecatedModelNames { get; set; }
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
        public int InferenceBookmark { get; set; }
        public string[] IgnoredEvents { get; set; }
        [Required]
        public int LinkedLogInputID { get; set; }
        [Required]
        public virtual LogInput LinkedLogInput { get; set; }
    }
}
