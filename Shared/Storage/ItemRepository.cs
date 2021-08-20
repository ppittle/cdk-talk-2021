﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Shared.Storage
{
    public static class Constants
    {
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

        public Task Upsert(ItemDataModel itemDataModel)
        {
            throw new NotImplementedException();
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

        private void Serialize(ItemDataModel model)
        {

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
