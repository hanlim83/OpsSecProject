using System.ComponentModel.DataAnnotations;

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
