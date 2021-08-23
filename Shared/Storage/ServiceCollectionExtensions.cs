using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;
using Shared.Ingestion;

namespace Shared.Storage
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCdkTalk(this IServiceCollection services, AWSOptions awsOptions)
        {
            services.AddDefaultAWSOptions(awsOptions);

            services.AddSingleton<IIngestionQueueClient, IngestionQueueClient>();
            services.AddSingleton<IItemRepository, ItemRepository>();
            services.AddAWSService<IAmazonDynamoDB>();
            services.AddAWSService<IAmazonSQS>();

            return services;
        }
    }
}
