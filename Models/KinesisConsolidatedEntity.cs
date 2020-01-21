using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public class KinesisConsolidatedEntity
    {
        public int ID { get; set; }
        [Required]
        public string PrimaryFirehoseStreamName { get; set; }
        public string SecondaryFirehoseStreamName { get; set; }
        public string AnalyticsApplicationName { get; set; }
        [Required]
        public bool? AnalyticsEnabled { get; set; }
        [Required]
        public int LinkedLogInputID { get; set; }
        [Required]
        public virtual LogInput LinkedLogInput { get; set; }
    }
}
