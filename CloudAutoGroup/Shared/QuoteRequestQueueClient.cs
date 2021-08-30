using System;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace CloudAutoGroup.TVCampaign.Shared
{
    public interface IQuoteRequestQueueClient
    {
        Task Write(QuoteRequest message);
    }

    public class QuoteRequestQueueClientSettings
    {
        public string QueueUrl { get; set; }
    }

    public class QuoteRequestQueueClient : IQuoteRequestQueueClient
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly IOptions<QuoteRequestQueueClientSettings> _options;

        public QuoteRequestQueueClient(IAmazonSQS sqsClient, IOptions<QuoteRequestQueueClientSettings> options)
        {
            _sqsClient = sqsClient;
            _options = options;
        }

        public async Task Write(QuoteRequest message)
        {
            await _sqsClient.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = _options.Value.QueueUrl,
                MessageBody = JsonConvert.SerializeObject(message)
            });
        }
    }
}