using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudAutoGroup.TVCampaign.Shared;

namespace CloudAutoGroup.TVCampaign.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuotesController : ControllerBase
    {
        private readonly IFullQuoteRepository _quoteRepository;

        public QuotesController(IFullQuoteRepository quoteRepository)
        {
            _quoteRepository = quoteRepository;
        }

        // GET: api/Quotes/
        [HttpGet]
        public async Task<IEnumerable<FullQuote>> Get()
        {
            return await _quoteRepository.GetAll();
        }
    }
}
