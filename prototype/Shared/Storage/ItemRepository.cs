using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;

namespace Shared.Storage
{
    public static class Constants
    {
        // TODO, let CDK generate table name?  that way multiple 'stacks' can be deployed in the same region
        public const string ItemTableName = "items";
    }

    public interface IItemRepository
    {
        Task Upsert(ItemDataModel itemDataModel);
        Task<List<ItemDataModel>> GetAll(int customerId);
    }

    internal class ItemRepository : IItemRepository
    {
        private readonly IAmazonDynamoDB _dynamoDb;

        public ItemRepository(IAmazonDynamoDB dynamoDb)
        {
            _dynamoDb = dynamoDb;
        }

        public async Task Upsert(ItemDataModel itemDataModel)
        {
            await _dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = Constants.ItemTableName,
                Item = Serialize(itemDataModel)
            });
        }

        public async Task<List<ItemDataModel>> GetAll(int customerId)
        {
            var request = new QueryRequest
            {
                TableName = Constants.ItemTableName,
                KeyConditionExpression = $"{nameof(ItemDataModel.CustomerId)} = :v_customerId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    {":v_customerId", new AttributeValue {N = customerId.ToString()}}
                }
            };

            // new Amazon.DynamoDBv2.DataModel.DynamoDBContext(_dynamoDb).QueryAsync
            // or
            // Amazon.DynamoDBv2.DocumentModel.Document.FromAttributeMap().to

            return
                await _dynamoDb
                    .Paginators
                    .Query(request)
                    .Responses
                    .SelectMany(x =>
                        x.Items
                            .Select(y => Deserialize(y))
                            .ToAsyncEnumerable())
                    .ToListAsync();
        }

        private Dictionary<string, AttributeValue> Serialize(ItemDataModel model)
        {


            // TODO is there a better way?
            return new Dictionary<string, AttributeValue>
            {
                {nameof(ItemDataModel.CustomerId), new AttributeValue{ N = model.CustomerId.ToString()} },
                {nameof(ItemDataModel.CreatedDate), new AttributeValue{ S = model.CreatedDate.ToString("O")} },
                {nameof(ItemDataModel.Id), new AttributeValue{ S = model.Id.ToString()} },
                {nameof(ItemDataModel.ContainsHelloWorld), new AttributeValue{ BOOL = model.ContainsHelloWorld} },
                {nameof(ItemDataModel.IsPalindrome), new AttributeValue{ BOOL = model.IsPalindrome} },
                {nameof(ItemDataModel.ItemData), new AttributeValue{ S = model.ItemData} }
            };
        }

        private ItemDataModel Deserialize(Dictionary<string, AttributeValue> item)
        {
            

            // TODO is there a better way?
            var model = new ItemDataModel();

            if (item.TryGetValue(nameof(ItemDataModel.CustomerId), out var customer))
                model.CustomerId = int.Parse(customer.N);

            if (item.TryGetValue("Id", out var id))
                model.Id = Guid.Parse(id.S);

            if (item.TryGetValue("ItemData", out var data))
                model.ItemData= data.S;

            if (item.TryGetValue("ContainsHelloWorld", out var helloWorld))
                model.ContainsHelloWorld = helloWorld.BOOL;

            if (item.TryGetValue("IsPalindrome", out var palindrome))
                model.IsPalindrome = palindrome.BOOL;

            

            return model;
        }
    }
}
