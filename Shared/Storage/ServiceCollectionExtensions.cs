using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Storage
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCdkTalk(this IServiceCollection services, AWSOptions awsOptions)
        {
            services.AddDefaultAWSOptions(awsOptions);

            services.AddSingleton<IItemRepository, ItemRepository>();
            services.AddAWSService<IAmazonDynamoDB>();

            return services;
        }
    }
}
