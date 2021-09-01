using Amazon.CDK;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Cdk
{
    public class DeploymentSettings
    {
        public string AwsAccountId { get; set; }
        public string AwsRegion { get; set; }
        public double RequestQuoteProcessorMemorySize { get; set; }
    }

    sealed class Program
    {
        public static void Main(string[] args)
        {
            // var settings
            var deploymentSettings =
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        {"AwsAccountId", "TODO - Enter your AWS Account Id here"},
                        {"AwsRegion", "eu-west-1"},
                        {"RequestQuoteProcessorMemorySize", "128"}
                    })
                    // relative to the root of the Cdk.csproj
                    .AddJsonFile("settings.json", optional: true)
                    // could also add environment variables, or
                    // any other Configuration Builder
                    .Build()
                    .Get<DeploymentSettings>();

            var app = new App();
            new TVCampaignStack(deploymentSettings, app, "TVCampaignStack", new StackProps
            {
                // If you don't specify 'env', this stack will be environment-agnostic.
                // Account/Region-dependent features and context lookups will not work,
                // but a single synthesized template can be deployed anywhere.

                // Uncomment the next block to specialize this stack for the AWS Account
                // and Region that are implied by the current CLI configuration.
                /*
                Env = new Amazon.CDK.Environment
                {
                    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                    Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION"),
                }
                */

                // Uncomment the next block if you know exactly what Account and Region you
                // want to deploy the stack to.
                /**/
                Env = new Amazon.CDK.Environment
                {
                    Account = deploymentSettings.AwsAccountId,
                    Region = deploymentSettings.AwsRegion
                }

                // For more information, see https://docs.aws.amazon.com/cdk/latest/guide/environments.html
            });

            app.Synth();
        }
    }
}
