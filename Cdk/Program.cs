using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Amazon.CDK;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Environment = Amazon.CDK.Environment;

namespace Cdk
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            
            // example of how to use .net core configuration
            var deploymentSettings =
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        {"WebHostInstanceCount", "1"},
                        {"WebHostCpuCount", "512"},
                        {"WebHostMemoryLimit", "1024"},
                    })
                    .AddEnvironmentVariables()
                    // relative to the root of the Cdk.csproj
                    // TODO: Recommended way to bring in settings?
                    .AddJsonFile("settings.json", optional: false)
                    // could also add environment variables, or
                    // any other Configuration Builder
                    .Build()
                    .Get<DeploymentSettings>();

            // TODO: Recommended way to debug?
            if (deploymentSettings.AttachDebugger  || app.Node.TryGetContext("debug")?.ToString()  == "true")
            {
                Debugger.Launch();

                Console.WriteLine($"Waiting for Debugger: {Process.GetCurrentProcess().Id}");
                while (!Debugger.IsAttached)
                    Thread.Sleep(TimeSpan.FromMilliseconds(500));
                
            }

            new AppStack(deploymentSettings, app, "Cdk-Talk-AppStack-beanstalk", new StackProps
            {
                Env = new Environment
                {
                    Account = deploymentSettings.AwsAccountId,
                    Region = deploymentSettings.AwsRegion
                }
            });
            app.Synth();

            // TODO Pre-Seed dynamo db with some data.
        }
    }
}
