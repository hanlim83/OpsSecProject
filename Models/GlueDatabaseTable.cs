using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public class GlueDatabaseTable
    {
        public int ID { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string Schema { get; set; }
        [Required]
        public string timestampField { get; set; }
        [Required]
        public int LinkedDatabaseID { get; set; }
        [Required]
        public virtual GlueDatabase LinkedDatabase { get; set; }
        [Required]
        public int LinkedGlueConsolidatedInputEntityID { get; set; }
        [Required]
        public virtual GlueConsolidatedEntity LinkedGlueConsolidatedInputEntity { get; set; }
    }
}
