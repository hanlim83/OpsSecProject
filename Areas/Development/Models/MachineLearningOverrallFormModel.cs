using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Areas.Development.Models
{
    public class MachineLearningOverrallFormModel
    {
        [Required]
        public string jobName { get; set; }
        [Required]
        public string s3InputUri { get; set; }
        [Required]
        public string s3OutputPath { get; set; }
        [Required]
        public string configName { get; set; }
        [Required]
        public string endpointName { get; set; }
        [Required]
        public string modelName { get; set; }
    }
}
