using IpStack.Models;
using OpsSecProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpsSecProject.ViewModels
{
    public class QuestionableEventReviewViewModel
    {
        public QuestionableEvent ReviewEvent { get;set; }
        public IpAddressDetails SupplmentaryInformation { get; set; }
    }
}
