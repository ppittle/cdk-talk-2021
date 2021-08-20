using Microsoft.AspNetCore.Mvc;
using Shared.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Web.Controllers
{
    /// <summary>
    /// Can only read items.  To write items, send a request to the IngestionLambda
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ItemController : ControllerBase
    {
        private readonly IItemRepository _itemRepository;

        public ItemController(IItemRepository itemRepository)
        {
            _itemRepository = itemRepository;
        }

        // GET: api/<ItemController>/5
        [HttpGet("{customerId}")]
        public async Task<IEnumerable<ItemDataModel>> Get(int customerId)
        {
            return await _itemRepository.GetAll(customerId);
        }
    }
}
