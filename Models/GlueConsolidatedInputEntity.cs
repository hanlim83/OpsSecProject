using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace OpsSecProject.Models
{
    public class GlueConsolidatedInputEntity
    {
        public int ID { get; set; }
        [Required]
        public string CrawlerName { get; set; }
        public string JobName { get; set; }
        [Required]
        public string DBConnectionName { get; set; }
        public string JobScriptLocation { get; set; }
        [Required]
        public int LinkedLogInputID { get; set; }
        [Required]
        public virtual LogInput LinkedLogInput { get; set; }
        [Required]
        public virtual GlueDatabaseTable LinkedTable { get; set; }
    }
}
