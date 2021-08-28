using System.Diagnostics;
using System.IO;
using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Lambda;
using RequestQuoteApiFunction = CloudAutoGroup.TVCampaign.RequestQuoteApi.Functions;
using RequestQuoteProcessorFunction = CloudAutoGroup.TVCampaign.RequestQuoteProcessor.Function;

namespace Cdk
{
    public class TVCampaignStack : Stack
    {
        private const string _buildConfiguration =
#if DEBUG
            "Debug";
#else
            "Release";
#endif

        internal TVCampaignStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            CreateRequestQuoteApiHost();

            CreateRequestQuoteQueueProcessorHost();
        }

        /// <summary>
        /// Dotnet 5 Container Image Lambda + Api Gateway
        /// </summary>
        private void CreateRequestQuoteApiHost()
        {
            // relative to cdk.json file
            var directoryContainingDockerFile = nameof(CloudAutoGroup.TVCampaign.RequestQuoteApi);

            var requestQuoteApiLambda = new Function(this, "request-quote-api-lambda", new FunctionProps
            {
                Runtime = Runtime.FROM_IMAGE,
                Code = Code.FromAssetImage(directoryContainingDockerFile, new AssetImageCodeProps
                {
                    //Assembly::Type::Method
                    Cmd = new[]
                    {
                        // Assembly
                        $"{typeof(RequestQuoteApiFunction).Assembly.GetName().Name}::" +
                        // Full Type
                        $"{typeof(RequestQuoteApiFunction).FullName}::" +
                        // Method
                        $"{nameof(RequestQuoteApiFunction.Get)}"
                    }
                }),
                Handler = Handler.FROM_IMAGE,
                Timeout = Duration.Seconds(15)
            });

            // setup api gateway
            var api = new LambdaRestApi(this, "request-quote-api-lambda-api-gateway", new LambdaRestApiProps
            {
                Handler = requestQuoteApiLambda
            });
        }

        /// <summary>
        /// Dotnet 3.1 Lambda
        /// </summary>
        private void CreateRequestQuoteQueueProcessorHost()
        {
            var processingLambda = new Function(this, "request-quote-processor-lambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_CORE_3_1,
                Code = Code.FromAsset(
                    $"{nameof(CloudAutoGroup.TVCampaign.RequestQuoteProcessor)}/bin/{_buildConfiguration}/netcoreapp3.1/publish"),
                
                // Assembly::Type::Method
                Handler = 
                    // Assembly
                    $"{typeof(RequestQuoteProcessorFunction).Assembly.GetName().Name}::" +
                    // Full Type
                    $"{typeof(RequestQuoteProcessorFunction).FullName}::" +
                    // Method
                    $"{nameof(RequestQuoteProcessorFunction.FunctionHandler)}",

                Timeout = Duration.Seconds(30)
            });
        }
    }
}
