using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;

namespace CloudAutoGroup.TVCampaign.Shared
{
    public interface IFullQuoteRepository
    {
        Task Insert(FullQuote quote);
        Task<List<FullQuote>> GetAll();
    }

    public class FullQuoteRepositorySettings
    {
        public string TableName { get; set; }
    }

    public class FullQuoteRepository : IFullQuoteRepository
    {
        private readonly IOptions<FullQuoteRepositorySettings> _settings;
        private readonly IAmazonDynamoDB _dynamoDb;

        public FullQuoteRepository(IOptions<FullQuoteRepositorySettings> settings, IAmazonDynamoDB dynamoDb)
        {
            _settings = settings;
            _dynamoDb = dynamoDb;
        }

        public async Task Insert(FullQuote quote)
        {
            await _dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = _settings.Value.TableName,
                Item = Serialize(quote)
            });
        }

        public Task<List<FullQuote>> GetAll()
        {
            throw new NotImplementedException();
        }

        private Dictionary<string, AttributeValue> Serialize(FullQuote model)
        {
            return new Dictionary<string, AttributeValue>
            {
                {nameof(FullQuote.Request.Name), new AttributeValue{ S = model.Request.Name} },
                {nameof(FullQuote.Request.Email), new AttributeValue{ S = model.Request.Email} },
                {nameof(FullQuote.Request.CarType), new AttributeValue{ S = model.Request.CarType} },
                {nameof(FullQuote.Request.CreditScoreEstimate), new AttributeValue{ N = model.Request.CreditScoreEstimate.ToString()} },
                {nameof(FullQuote.MonthlyPremium), new AttributeValue{ N = model.MonthlyPremium.ToString()} }
            };
        }

    }
}