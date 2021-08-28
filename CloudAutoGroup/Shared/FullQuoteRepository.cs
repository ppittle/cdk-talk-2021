using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CloudAutoGroup.TVCampaign.Shared
{
    public interface IFullQuoteRepository
    {
        Task Insert(FullQuote quote);
        Task<List<FullQuote>> GetAll();
    }

    public class FullQuoteRepository : IFullQuoteRepository
    {
        public Task Insert(FullQuote quote)
        {
            throw new NotImplementedException();
        }

        public Task<List<FullQuote>> GetAll()
        {
            throw new NotImplementedException();
        }

    }
}