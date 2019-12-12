using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public class Settings
    {
        public int ID { get; set; }
        [Required]
        public bool Always2FA { get; set; }

        [Required]
        public int LinkedUserID { get; set; }
        [Required]
        public virtual User LinkedUser { get; set; }
    }
}
