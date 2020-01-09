using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace OpsSecProject.Models
{
    public class GlueDatabase
    {
        public int ID { get; set; }
        [Required]
        public string Name { get; set; }
        public virtual ICollection<GlueDatabaseTable> Tables { get; set; }
    }
}
