using System;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Newtonsoft.Json;
using Shared.Ingestion;
using Shared.Storage;

namespace ProcessingLambda
{
    public class App
    {
        private readonly IItemRepository _itemRepository;

        public App(IItemRepository itemRepository)
        {
            _itemRepository = itemRepository;
        }

        public async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            context.Logger.LogLine($"[{GetType().FullName}] Processing message {message.Body}");

            try
            {
                var request = JsonConvert.DeserializeObject<IngestionMessage>(message.Body);

                await _itemRepository.Upsert(ProcessItemData(request));

                context.Logger.LogLine($"[{GetType().FullName}] Processed message {message.Body}");
            }
            catch (Exception e)
            {
                context.Logger.LogLine($"[{GetType().FullName}] Error on message {message.Body}.  {JsonConvert.SerializeObject(e)}");
            }
        }

        private ItemDataModel ProcessItemData(IngestionMessage message)
        {
            var data = message.Item.Data;

            var dataModel = new ItemDataModel
            {
                CustomerId = message.CustomerId,
                ItemData = data
            };

            // simulate 'intense processing'
            dataModel.ContainsHelloWorld = data.Contains("hello world", StringComparison.OrdinalIgnoreCase);
            dataModel.IsPalindrome = data.ToCharArray().SequenceEqual(data.ToCharArray().Reverse());

            return dataModel;
        }
    }
}