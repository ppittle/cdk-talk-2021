using System;
using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.ElasticBeanstalk;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.SQS;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;

namespace Cdk
{
    public class AppStack : Stack
    {
        internal AppStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            // Step 1: Create Everything
            var queue = CreateSQS();

            var dynamoDb = CreateDynamoDb();

            var ingestionLambda = CreateIngestionLambda(queue);

            var processingLambda = CreateProcessingLambda(queue, dynamoDb);

            var website = CreateElasticBeanstalkWeb(ingestionLambda, dynamoDb);
        }

        private Queue CreateSQS()
        {
            return new Amazon.CDK.AWS.SQS.Queue(this, "queue", new QueueProps
            {
                QueueName = "ingestion-queue"
            });
        }

        private Table CreateDynamoDb()
        {
            var table = new Amazon.CDK.AWS.DynamoDB.Table(this, "item-storage", new TableProps
            {
                TableName = "items",
                PartitionKey = new Attribute
                {
                    Name = "id",
                    Type = AttributeType.STRING
                },
                SortKey = new Attribute
                {
                    Name = "id",
                    Type = AttributeType.STRING
                }
            });

            return table;
        }

        /// <summary>
        /// Dotnet 3.1 Lambda
        /// </summary>
        private Function CreateProcessingLambda(Queue ingestionQueue, Table itemsTable)
        {
            const string buildConfiguration =
                #if DEBUG
                "Debug";
                #else
                "Release";
                #endif

            var processingLambda = new Function(this, "processing-lambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_CORE_3_1,
                Code = Code.FromAsset($"{nameof(IngressLambda)}/bin/${buildConfiguration}/netcoreapp3.1/publish"),

                // Assembly::Type::Method
                Handler = $"{new ProcessingLabmda.Function().GetType().Assembly.GetName().Name}::{nameof(ProcessingLabmda.Function)}::{nameof(ProcessingLabmda.Function.FunctionHandler)}"
            });

            // Setup Permissions
            ingestionQueue.GrantConsumeMessages(processingLambda);

            itemsTable.GrantReadData(processingLambda);

            return processingLambda;
        }

        /// <summary>
        /// Dotnet 5 Container Image Lambda + Api Gateway
        /// </summary>
        private Function CreateIngestionLambda(Queue ingestionQueue)
        {
            var dotnet5Lambda = new Function(this, "ingestion-lambda", new FunctionProps
            {
                Runtime = Runtime.FROM_IMAGE,
                Code = Code.FromAssetImage(nameof(IngressLambda), new AssetImageCodeProps
                {
                    //Assembly::Type::Method
                    Cmd = new[]
                    {
                        $"{new IngressLambda.Functions().GetType().Assembly.GetName().Name}::{nameof(IngressLambda.Functions)}::{nameof(IngressLambda.Functions.Get)}"
                    }
                }),
                Handler = Handler.FROM_IMAGE
            });

            // setup api gateway
            new LambdaRestApi(this, "ingestion-lambda-api-gateway", new LambdaRestApiProps
            {
                Handler = dotnet5Lambda
            });

            // grant write access to queue
            ingestionQueue.GrantSendMessages(dotnet5Lambda);

            return dotnet5Lambda;
        }
        

        private CfnApplication CreateElasticBeanstalkWeb(object ingestionLambda, object dynamoDb)
        {
            return new CfnApplication(this, "website", new CfnApplicationProps
            {
                ApplicationName = "web"
            });
        }
    }
}
