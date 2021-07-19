using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Storage
{
    public interface IItemRepository
    {
        Task Upsert(ItemDataModel itemDataModel);
        Task<List<ItemDataModel>> GetAll();
    }

    internal class ItemRepository : IItemRepository
    {
        public Task Upsert(ItemDataModel itemDataModel)
        {
            throw new NotImplementedException();
        }

        public Task<List<ItemDataModel>> GetAll()
        {
            throw new NotImplementedException();
        }
    }
}
