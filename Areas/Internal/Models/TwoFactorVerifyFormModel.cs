using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Areas.Internal.Models
{
    public class TwoFactorVerifyFormModel{

        [Required]
        public string Code { get; set; }
        [Required]
        public string recaptchaResponse { get; set; }

        public string ReturnUrl { get; set; }
    }
}
