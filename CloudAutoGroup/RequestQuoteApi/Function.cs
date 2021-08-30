using System;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.SQS;
using CloudAutoGroup.TVCampaign.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace CloudAutoGroup.TVCampaign.RequestQuoteApi
{
    public class Functions
    {
        private readonly App _app;

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public Functions()
        {
            var config =
                new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .Build();

            var services =
                new ServiceCollection()
                    .AddTransient<IQuoteRequestQueueClient, QuoteRequestQueueClient>()
                    .AddAWSService<IAmazonSQS>()
                    .AddTransient<App>();

            services.AddOptions();
            services.Configure<QuoteRequestQueueClientSettings>(config);

            _app = services.BuildServiceProvider().GetService<App>();
        }


        /// <summary>
        /// A Lambda function to respond to HTTP Get methods from API Gateway
        /// </summary>
        /// <param name="request"></param>
        /// <returns>The API Gateway response.</returns>
        public async Task<APIGatewayProxyResponse> Get(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return await _app.Get(request, context);
        }
    }
}
