using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsSecProject.Data;
using OpsSecProject.Models;
using System.Linq;

namespace OpsSecProject.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class SecurityController : Controller
    {

        private readonly AccountContext _Acontext;
        private readonly SecurityContext _Scontext;

        public SecurityController(AccountContext Acontext, SecurityContext Scontext)
        {
            _Acontext = Acontext;
            _Scontext = Scontext;
        }

        public IActionResult Index()
        {
            SecurityOverallViewModel model = new SecurityOverallViewModel
            {
                Activites = _Scontext.Activities.ToList(),
                allUsers = _Acontext.Users.ToList()
            };
            return View(model);
        }
    }
}