using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public enum CommmuicationOptions
    {
        EMAIL,SMS
    }
    public class Settings
    {
        public int ID { get; set; }
        [Required]
        public bool Always2FA { get; set; }
        [Required]
        public CommmuicationOptions CommmuicationOptions{ get; set; }
        [Required]
        public bool AutoTrain { get; set; }
        [Required]
        public int LinkedUserID { get; set; }
        [Required]
        public virtual User LinkedUser { get; set; }
    }
}
