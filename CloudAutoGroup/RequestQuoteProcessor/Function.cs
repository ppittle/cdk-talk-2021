using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using CloudAutoGroup.TVCampaign.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace CloudAutoGroup.TVCampaign.RequestQuoteProcessor
{
    public class Function
    {
        private readonly App _app;

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {
            var config =
                new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .Build();

            var services =
                new ServiceCollection()
                    .AddTransient<IFullQuoteRepository, FullQuoteRepository>()
                    .AddTransient<INewQuoteNotifier, NewQuoteNotifier>()
                    .AddAWSService<IAmazonDynamoDB>()
                    .AddAWSService<IAmazonSimpleNotificationService>()
                    .AddTransient<App>();

            services.AddOptions();
            services.Configure<FullQuoteRepositorySettings>(config);
            services.Configure<NewQuoteNotifierSettings>(config);

            _app = services.BuildServiceProvider().GetService<App>();
        }


        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
        /// to respond to SQS messages.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
        {
            foreach (var message in evnt.Records)
            {
                await _app.ProcessMessageAsync(message, context);
            }
        }
    }
}
