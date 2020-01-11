using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.ViewModels
{
    public class RealmDiscoveryModel
    {
        [Required]
        public string Username { get; set; }
        [Required]
        public string recaptchaResponse {get;set;}
        public string ReturnUrl { get; set; }
    }
}
