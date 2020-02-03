using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public class GlueConsolidatedEntity
    {
        public int ID { get; set; }
        [Required]
        public string CrawlerName { get; set; }
        public string JobName { get; set; }
        [Required]
        public int LinkedLogInputID { get; set; }
        [Required]
        public virtual LogInput LinkedLogInput { get; set; }
        public virtual GlueDatabaseTable LinkedTable { get; set; }
    }
}
