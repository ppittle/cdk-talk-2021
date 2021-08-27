using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Ingestion;
using Shared.Storage;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace IngressLambda
{
    /// <summary>
    /// TODO dleete aws-lambda-tools-defaults.json and serverless.template
    /// </summary>
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
                    .AddCdkTalk()
                    .AddTransient<App>();

            services.AddOptions();
            services.Configure<IngestionQueueSettings>(config);

            _app = services.BuildServiceProvider().GetService<App>();
        }
        
        /// <summary>
        /// ********************
        /// 1.  Api Endpoint is Not Called Frequently and Kicks off a longer running background process, so Lambda is a good fit
        /// 2.  Do a rename as part of the talk to show power of directly referencing method name in CDK?
        ///       WARNING: Call out a) don't do this on storage b) be careful with green/blue deployments.  This helps keep things in sync, but be careful when renaming resources.
        ///           ie this is before we go live :)
        /// ****************
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
