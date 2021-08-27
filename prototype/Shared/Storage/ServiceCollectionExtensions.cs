using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;
using Shared.Ingestion;

namespace Shared.Storage
{
    public static class ServiceCollectionExtensions
    {
        // TODO - inline only what's needed, instead of having an abstraction
        public static IServiceCollection AddCdkTalk(this IServiceCollection services)
        {
            services.AddSingleton<IIngestionQueueClient, IngestionQueueClient>();
            services.AddSingleton<IItemRepository, ItemRepository>();

            // rely on using the default aws credential fallback
            services.AddAWSService<IAmazonDynamoDB>();
            services.AddAWSService<IAmazonSQS>();

            return services;
        }
    }
}
