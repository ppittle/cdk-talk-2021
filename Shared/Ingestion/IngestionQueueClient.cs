using System;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Shared.Ingestion
{
    public interface IIngestionQueueClient
    {
        Task Write(IngestionMessage message);
        Task<IngestionMessage> Read();
    }
    
    internal class IngestionQueueClient : IIngestionQueueClient
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly IOptions<IngestionQueueSettings> _options;

        public IngestionQueueClient(IAmazonSQS sqsClient, IOptions<IngestionQueueSettings> options)
        {
            _sqsClient = sqsClient;
            _options = options;
        }

        public async Task Write(IngestionMessage message)
        {
            await _sqsClient.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = _options.Value.QueueUrl,
                MessageBody = JsonConvert.SerializeObject(message)
            });
        }

        public Task<IngestionMessage> Read()
        {
            throw new NotImplementedException();
        }
    }
}
