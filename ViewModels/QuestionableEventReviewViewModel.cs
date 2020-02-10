using IpStack.Models;
using OpsSecProject.Models;

namespace OpsSecProject.ViewModels
{
    public class QuestionableEventReviewViewModel
    {
        public QuestionableEvent ReviewEvent { get;set; }
        public IpAddressDetails SupplmentaryInformation { get; set; }
    }
}
