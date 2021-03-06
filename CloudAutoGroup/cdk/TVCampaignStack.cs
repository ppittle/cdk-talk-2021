using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.ElasticBeanstalk;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.S3.Assets;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using CloudAutoGroup.TVCampaign.Shared;
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
        private readonly DeploymentSettings _settings;

        internal TVCampaignStack(DeploymentSettings settings, Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            _settings = settings;

            var queue = CreateQueue();
            
            var dataStore = CreateDataStore();

            var newQuoteNotifier = CreateNewQuoteDingEmailNotifier();

			var requestApi = CreateRequestQuoteApiHost(queue);

            CreateRequestQuoteQueueProcessorHost(queue, dataStore, newQuoteNotifier);

            CreateWebsiteHost(requestApi, dataStore);
        }

        private Topic CreateNewQuoteDingEmailNotifier()
        {
            var snsTopic = new Amazon.CDK.AWS.SNS.Topic(this, "sales-ding-notifier", new TopicProps
            {
                DisplayName = "Sales Ding Notifier",
                TopicName = "sales-ding-notifier"
            });

            snsTopic.AddSubscription(new EmailSubscription(_settings.MarketingDingEmail));

            return snsTopic;
        }

        private Queue CreateQueue()
        {
            return new Amazon.CDK.AWS.SQS.Queue(this, "queue", new QueueProps
            {
                QueueName = "request-queue"
            });
        }

        /// <summary>
        /// Create an Amazon DynamoDb table we can use to store quotes.
        /// </summary>
        private Table CreateDataStore()
        {
            var table = new Amazon.CDK.AWS.DynamoDB.Table(this, "item-storage", new TableProps
            {
                TableName = "quotes",
                BillingMode = BillingMode.PAY_PER_REQUEST,
                RemovalPolicy = RemovalPolicy.DESTROY,
                PartitionKey = new Attribute
                {
                    Name = nameof(FullQuote.Request.Name),
                    Type = AttributeType.STRING
                },
                SortKey = new Attribute
                {
                    Name = nameof(FullQuote.Request.Email),
                    Type = AttributeType.STRING
                }
            });

            return table;
        }

        /// <summary>
        /// Dotnet 5 Container Image Lambda + Api Gateway
        /// </summary>
        private LambdaRestApi CreateRequestQuoteApiHost(Queue quoteRequestQueue)
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
                Timeout = Duration.Seconds(15),
                Environment = new Dictionary<string, string>
                {
                    {nameof(QuoteRequestQueueClientSettings.QueueUrl), quoteRequestQueue.QueueUrl}
                }
            });

            // grant write access to queue
            quoteRequestQueue.GrantSendMessages(requestQuoteApiLambda);

            // setup api gateway
            var api = new LambdaRestApi(this, "request-quote-api-lambda-api-gateway", new LambdaRestApiProps
            {
                Handler = requestQuoteApiLambda,

                DefaultCorsPreflightOptions = new CorsOptions
                {
                    AllowHeaders = new []{ "Content-Type", "X-Amz-Date", "Authorization", "X-Api-Key" },
                    AllowMethods = new []{ "OPTIONS", "GET", "POST", "PUT", "PATCH", "DELETE" },
                    AllowCredentials =  true,
                    AllowOrigins = new []{ "*" }
                }
            });

            return api;
        }

        /// <summary>
        /// Dotnet 3.1 Lambda
        /// </summary>
        private void CreateRequestQuoteQueueProcessorHost(Queue quoteRequestQueue, Table dataStore, Topic newQuoteNotifier)
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

                Timeout = Duration.Seconds(30),
                MemorySize = _settings.RequestQuoteProcessorMemorySize,
                Environment = new Dictionary<string, string>
                {
                    {nameof(FullQuoteRepositorySettings.TableName), dataStore.TableName},
                    {nameof(NewQuoteNotifierSettings.NotificationTopicArn), newQuoteNotifier.TopicArn}
                }
            });

            // Configure lambda to process ingestion queue messages
            processingLambda.AddEventSource(new SqsEventSource(quoteRequestQueue));

            // Setup permissions
            dataStore.GrantWriteData(processingLambda);
            newQuoteNotifier.GrantPublish(processingLambda);
        }

        /// <summary>
        /// Dotnet 5 Elastic Beanstalk Website
        /// </summary>
        /// <param name="requestApi"></param>
        private void CreateWebsiteHost(LambdaRestApi requestApi, Table dataStore)
        {
            var deployAsset = new Asset(this, "webDeploy", new AssetProps
            {
                // Assume we're running at root solution with `cdk deploy`
                // also requires cdk command to publish web project
                Path = Path.Combine(Directory.GetCurrentDirectory(), @$"Web\bin\{_buildConfiguration}\net5.0\publish\")
            });
            
            var applicationVersion = new CfnApplicationVersion(this, "Web-ApplicationVersion", new CfnApplicationVersionProps
            {
                ApplicationName = $"{base.StackName}-web",
                SourceBundle = new CfnApplicationVersion.SourceBundleProperty
                {
                    S3Bucket = deployAsset.S3BucketName,
                    S3Key = deployAsset.S3ObjectKey
                }
            });
            
            var application = new CfnApplication(this, "Application", new CfnApplicationProps
            {
                ApplicationName = applicationVersion.ApplicationName
            });
            applicationVersion.AddDependsOn(application);
            
            var role = new Role(this, "Role", new RoleProps
            {
                AssumedBy = new ServicePrincipal("ec2.amazonaws.com"),

                // https://docs.aws.amazon.com/elasticbeanstalk/latest/dg/iam-instanceprofile.html
                ManagedPolicies = new[]
                {
                    ManagedPolicy.FromAwsManagedPolicyName("AWSElasticBeanstalkWebTier"),
                    ManagedPolicy.FromAwsManagedPolicyName("AWSElasticBeanstalkWorkerTier")
                }
            });

            var instanceProfile = new CfnInstanceProfile(this, "InstanceProfile", new CfnInstanceProfileProps
            {
                Roles = new[] { role.RoleName}
            });

            var optionSettingProperties = new List<CfnEnvironment.OptionSettingProperty>
            {
                new CfnEnvironment.OptionSettingProperty
                {
                    Namespace = "aws:autoscaling:launchconfiguration",
                    OptionName = "IamInstanceProfile",
                    Value = instanceProfile.AttrArn
                },

                // set custom environment variables
                new CfnEnvironment.OptionSettingProperty
                {
                    Namespace = "aws:elasticbeanstalk:application:environment",
                    OptionName = nameof(CloudAutoGroup.TVCampaign.Web.Settings.RequestQuoteApiUrl),
                    Value = requestApi.Url
                },
                new CfnEnvironment.OptionSettingProperty
                {
                    Namespace = "aws:elasticbeanstalk:application:environment",
                    OptionName = nameof(FullQuoteRepositorySettings.TableName),
                    Value = dataStore.TableName
                }
            };

            dataStore.GrantReadData(role);

            var environment = new CfnEnvironment(this, "Environment", new CfnEnvironmentProps
            {
                EnvironmentName = $"{base.StackName}-Web-Environment",
                ApplicationName = application.ApplicationName,
                // https://docs.aws.amazon.com/elasticbeanstalk/latest/platforms/platforms-supported.html#platforms-supported.dotnetlinux
                SolutionStackName = "64bit Amazon Linux 2 v2.2.5 running .NET Core",
                OptionSettings = optionSettingProperties.ToArray(),
                VersionLabel = applicationVersion.Ref
            });

            var output = new CfnOutput(this, "EndpointURL", new CfnOutputProps
            {
                Value = $"http://{environment.AttrEndpointUrl}/"
            });
        }
    }
}
