using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public class LoginFormModel
    {
        [Required]
        public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
