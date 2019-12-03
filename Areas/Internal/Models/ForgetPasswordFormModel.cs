using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Areas.Internal.Models
{
    public class ForgetPasswordFormModel
    {
        [Required]
        public string Username { get; set; }
        [Required]
        public string recaptchaResponse { get; set; }

        public string ReturnUrl { get; set; }
    }
}
