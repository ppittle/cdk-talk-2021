using System;
using System.Threading.Tasks;

namespace CloudAutoGroup.TVCampaign.Shared
{
    public interface IQuoteRequestQueueClient
    {
        Task Write(QuoteRequest message);
    }

    public class QuoteRequestQueueClient : IQuoteRequestQueueClient
    {
        public Task Write(QuoteRequest message)
        {
            throw new NotImplementedException();
        }
    }
}