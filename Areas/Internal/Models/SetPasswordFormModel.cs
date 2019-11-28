using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Areas.Internal.Models
{
    public class SetPasswordModel
    {
        public string Token { get; set; }
        [Required]
        [Display(Name = "New Password")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }
        [Required]
        [Compare("NewPassword")]
        [Display(Name = "Confirm New Password")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }
    }
}
