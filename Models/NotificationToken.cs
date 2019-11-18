using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public enum Mode
    {
        SMS, EMAIL
    }
    public enum Type
    {
        Reset, Verify
    }

    public class NotificationToken
    {
        public int ID { get; set; }
        [Required]
        public Mode Mode { get; set; }
        [Required]
        public Type Type { get; set; }
        [Required]
        public bool Vaild { get; set; }

        [Required]
        public virtual User LinkedUser { get; set; }
    }
}
