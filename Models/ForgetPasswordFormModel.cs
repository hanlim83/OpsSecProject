using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public class ForgetPasswordModel
    {
        [Required]
        public string Username { get; set; }
    }
}
