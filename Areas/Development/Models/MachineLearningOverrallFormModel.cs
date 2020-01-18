using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Areas.Development.Models
{
    public class MachineLearningOverrallFormModel
    {
        [Required]
        public string s3InputPath { get; set; }
        [Required]
        public string predictionInputContent { get; set; }
    }
}
