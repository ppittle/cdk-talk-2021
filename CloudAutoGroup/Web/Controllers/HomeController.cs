using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CloudAutoGroup.TVCampaign.Web.Models;
using Microsoft.Extensions.Options;

namespace CloudAutoGroup.TVCampaign.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IOptions<Settings> _options;

        public HomeController(ILogger<HomeController> logger, IOptions<Settings> options)
        {
            _logger = logger;
            _options = options;
        }

        public IActionResult Index()
        {
            return View(_options.Value);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
