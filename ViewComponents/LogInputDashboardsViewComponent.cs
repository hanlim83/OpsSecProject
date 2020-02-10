using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsSecProject.Data;
using OpsSecProject.Models;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OpsSecProject.ViewComponents
{
    public class LogInputDashboardsViewComponent : ViewComponent
    {
        private readonly LogContext _context;

        public LogInputDashboardsViewComponent(LogContext context)
        {
            _context = context;
        }
        public async Task<IViewComponentResult> InvokeAsync()
        {
            return View(await _context.LogInputs.ToListAsync());
        }
    }
}
