using System;
using System.Threading.Tasks;
using Shared.Ingestion;

namespace Shared
{
    public interface IIngestionQueueClient
    {
        Task Write(IngestionMessage message);
        Task<IngestionMessage> Read();
    }
    
    internal class IngestionQueueClient : IIngestionQueueClient
    {
        public Task Write(IngestionMessage message)
        {
            throw new NotImplementedException();
        }

        public Task<IngestionMessage> Read()
        {
            throw new NotImplementedException();
        }
    }
}
