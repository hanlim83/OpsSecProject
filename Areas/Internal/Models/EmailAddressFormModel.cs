using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Areas.Internal.Models
{
    public class EmailAddressFormModel
    {
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        [Required]
        [Display(Name = "New Email Address")]
        [DataType(DataType.EmailAddress)]
        public string EmailAddress { get; set; }
        [Required]
        public string recaptchaResponse { get; set; }
    }
}
