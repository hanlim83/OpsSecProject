using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace OpsSecProject.Models
{
    public class S3Bucket
    {
        public int ID { get; set; }
        [Required]
        public string Name { get; set; }
        public virtual LogInput LinkedLogInput { get; set; }
    }
}
