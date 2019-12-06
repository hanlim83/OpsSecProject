using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Areas.Internal.Models
{
    public class TwoFactorChooseFormModel
    {
        [Required]
        public string Username { get; set; }
        [Required]
        public string Method { get; set; }
        public string[] Methods = new[] { "SMS","Email" };
        public string ReturnUrl { get; set; }
    }
}
