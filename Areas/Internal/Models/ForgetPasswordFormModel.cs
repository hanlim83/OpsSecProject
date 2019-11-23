using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Areas.Internal.Models
{
    public class ForgetPasswordModel
    {
        [Required]
        public string Username { get; set; }
    }
}
