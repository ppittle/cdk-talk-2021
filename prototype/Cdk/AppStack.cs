using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.ElasticBeanstalk;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.S3.Assets;
using Amazon.CDK.AWS.SQS;
using Shared.Storage;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;

namespace Cdk
{
    public class AppStack : Stack
    {
        private const string _buildConfiguration =
#if DEBUG
            "Debug";
#else
            "Release";
#endif

        private readonly DeploymentSettings _settings;

        internal AppStack(DeploymentSettings settings, Construct scope, string id, IStackProps props = null) 
            : base(scope, id, props)
        {
            _settings = settings;

            // Step 1: Create Queue
            var queue = CreateQueue();

            var dynamoDb = CreateDataStore();

            var ingestionLambdaApi = CreateIngestionApiFunction(queue);

            var processingLambda = CreateProcessingFunction(queue, dynamoDb);

            var website = CreateElasticBeanstalk(ingestionLambdaApi, dynamoDb);

            // TODO call out how you'd probably want to use a Load Balancer and put website and lambda 
            // behind the same ALB.  This would use the same top level domain, and make it so CORS setup isn't necessary.
        }
        
        private Queue CreateQueue()
        {
            return new Amazon.CDK.AWS.SQS.Queue(this, "queue", new QueueProps
            {
                // todo - add stack name suffix
                QueueName = "ingestion-queue"
            });
        }

        /// <summary>
        /// Create an Amazon DynamoDb table we can use to store data.
        /// </summary>
        private Table CreateDataStore()
        {
            var table = new Amazon.CDK.AWS.DynamoDB.Table(this, "item-storage", new TableProps
            {
                // TODO - switch to "Items-{props.StackName}"
                TableName = Shared.Storage.Constants.ItemTableName,
                BillingMode = BillingMode.PAY_PER_REQUEST,  // <--- can call out auto scaling capability of dynamodb, delay tuning until we have a lot of usage
                RemovalPolicy = RemovalPolicy.DESTROY,
                PartitionKey = new Attribute
                {
                    Name = nameof(ItemDataModel.CustomerId),
                    Type = AttributeType.NUMBER
                },
                SortKey = new Attribute
                {
                    Name = nameof(ItemDataModel.CreatedDate),
                    Type = AttributeType.STRING
                }
            });

            return table;
        }

        /// <summary>
        /// Dotnet 5 Container Image Lambda + Api Gateway
        /// </summary>
        private LambdaRestApi CreateIngestionApiFunction(Queue ingestionQueue)
        {
            var dotnet5Lambda = new Function(this, "ingestion-lambda", new FunctionProps
            {
                Runtime = Runtime.FROM_IMAGE,
                Code = Code.FromAssetImage(nameof(IngressLambda), new AssetImageCodeProps
                {
                    //Assembly::Type::Method
                    Cmd = new[]
                    {
                        // Assembly
                        $"{typeof(IngressLambda.Functions).Assembly.GetName().Name}::" +
                        // Full Type
                        $"{typeof(IngressLambda.Functions).FullName}::" +
                        // Method
                        $"{nameof(IngressLambda.Functions.Get)}"
                    }
                }),
                Handler = Handler.FROM_IMAGE,
                Timeout = Duration.Seconds(15),
                Environment = new Dictionary<string, string>
                {
                    {nameof(Shared.Ingestion.IngestionQueueSettings.QueueUrl), ingestionQueue.QueueUrl}
                }
            });
            
            // grant write access to queue
            ingestionQueue.GrantSendMessages(dotnet5Lambda);
            
            // setup api gateway
            var api = new LambdaRestApi(this, "ingestion-lambda-api-gateway", new LambdaRestApiProps
            {
                Handler = dotnet5Lambda,
                // set up CORS (TODO: unsure if this is necessary.  it MUST be set in lambda, unclear if this section helps)
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
        private Function CreateProcessingFunction(Queue ingestionQueue, Table itemsTable)
        {
            var processingLambda = new Function(this, "processing-lambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_CORE_3_1,
                Code = Code.FromAsset($"{nameof(ProcessingLambda)}/bin/{_buildConfiguration}/netcoreapp3.1/publish"),
                
                // Assembly::Type::Method
                Handler = 
                    // Assembly
                    $"{typeof(ProcessingLambda.Function).Assembly.GetName().Name}::" +
                    // Full Type
                    $"{typeof(ProcessingLambda.Function).FullName}::" +
                    // Method
                    $"{nameof(ProcessingLambda.Function.FunctionHandler)}",

                Timeout = Duration.Seconds(30)
            });

            // Configure lambda to process ingestion queue messages
            processingLambda.AddEventSource(new SqsEventSource(ingestionQueue));

            // Setup permissions
            itemsTable.GrantWriteData(processingLambda);

            return processingLambda;
        }

        /// <summary>
        /// Host website in AWS Fargate
        /// </summary>
        /// <param name="ingestionLambda"></param>
        /// <param name="dynamoDb"></param>
        /// <returns></returns>
        private ApplicationLoadBalancedFargateService CreateWebSite(Table dynamoDb)
        {
            // define docker registry
            var ecrRepository = Repository.FromRepositoryName(this, "ECRRepository", "Web-Docker-Image");

            //var containerImage = ContainerImage.FromEcrRepository(ecrRepository, "latest");
            var containerImageAsset = new DockerImageAsset(this, "Web-Docker-Container", new DockerImageAssetProps
            {
                Directory = ".",
                File = "Web-Dockerfile",
                // set our own tag
                BuildArgs = new Dictionary<string, string>{ {"-t", "latest" } }
            });
            containerImageAsset.Repository = ecrRepository;

            var containerImage = ContainerImage.FromDockerImageAsset(containerImageAsset);
            
            // define Fargate Web Task
            var taskDefinition = new FargateTaskDefinition(this, "WebSiteTaskDefinition", new FargateTaskDefinitionProps
            {
                Cpu = _settings.WebHostCpuCount,
                MemoryLimitMiB = _settings.WebHostMemoryLimit,
                TaskRole = new Role(this, "TaskRole", new RoleProps
                {
                    AssumedBy = new ServicePrincipal("ecs-tasks.amazonaws.com")
                })
            }){

            };
                
            taskDefinition
                // attach docker image
                .AddContainer("Container", new ContainerDefinitionOptions
                {
                    Image = containerImage
                })
                // and open http ports
                .AddPortMappings(new PortMapping
                {
                    ContainerPort = 80,
                    Protocol = Amazon.CDK.AWS.ECS.Protocol.TCP
                });

            // define Fargate Hosting
            var webSite = new ApplicationLoadBalancedFargateService(this, "FargateService", new ApplicationLoadBalancedFargateServiceProps
            {
                Cluster = new Cluster(this, "Cluster", new ClusterProps
                {
                    Vpc = Vpc.FromLookup(this, "Vpc", new VpcLookupOptions{ IsDefault = true }),
                    //Vpc = new Vpc(this, "App-Vpc"),
                    ClusterName = "WebSiteCluster",
                }),
                TaskDefinition = taskDefinition,
                DesiredCount = _settings.WebHostInstanceCount,
                ServiceName = "WebSite",
                AssignPublicIp = true,
                //SecurityGroups = ecsServiceSecurityGroups.ToArray()
            });
            
            dynamoDb.GrantReadData(webSite.Service.TaskDefinition.TaskRole);

            //todo, write configuration of Ingestion Lambda api endpoint.

            return webSite;
        }

        private CfnEnvironment CreateElasticBeanstalk(LambdaRestApi ingestionLambdaApi, Table dynamoDb)
        {
            var deployAsset = new Asset(this, "webDeploy", new AssetProps
            {
                // Assume we're running at root solution with `cdk deploy`
                // also requires cdk command to publish web project
                Path = Path.Combine(Directory.GetCurrentDirectory(), @$"Web\bin\{_buildConfiguration}\net5.0\publish\")
            });
            
            var applicationVersion = new CfnApplicationVersion(this, "Web-ApplicationVersion", new CfnApplicationVersionProps
            {
                ApplicationName = "web",
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
                Roles = new[]
                {
                    role.RoleName
                }
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
                    OptionName = nameof(Web.Settings.IngestionApiUrl),
                    Value = ingestionLambdaApi.Url
                }


                /*,
                new CfnEnvironment.OptionSettingProperty {
                     Namespace = "aws:elasticbeanstalk:environment",
                     OptionName =  "EnvironmentType",
                     Value = settings.EnvironmentType
                },
                new CfnEnvironment.OptionSettingProperty
                {
                     Namespace = "aws:elasticbeanstalk:managedactions",
                     OptionName = "ManagedActionsEnabled",
                     Value = settings.ElasticBeanstalkManagedPlatformUpdates.ManagedActionsEnabled.ToString().ToLower()
                }*/
            };
            
            /*
            if(!string.IsNullOrEmpty(settings.InstanceType))
            {
                optionSettingProperties.Add(new CfnEnvironment.OptionSettingProperty
                {
                    Namespace = "aws:autoscaling:launchconfiguration",
                    OptionName = "InstanceType",
                    Value = settings.InstanceType
                });
            }

            if (settings.EnvironmentType.Equals(ENVIRONMENTTYPE_LOADBALANCED))
            {
                optionSettingProperties.Add(
                    new CfnEnvironment.OptionSettingProperty
                    {
                        Namespace = "aws:elasticbeanstalk:environment",
                        OptionName = "LoadBalancerType",
                        Value = settings.LoadBalancerType
                    }
                );
            }

            if (!string.IsNullOrEmpty(settings.EC2KeyPair))
            {
                optionSettingProperties.Add(
                    new CfnEnvironment.OptionSettingProperty
                    {
                        Namespace = "aws:autoscaling:launchconfiguration",
                        OptionName = "EC2KeyName",
                        Value = settings.EC2KeyPair
                    }
                );
            }*/

            var environment = new CfnEnvironment(this, "Environment", new CfnEnvironmentProps
            {
                EnvironmentName = "Web-Environment",
                ApplicationName = application.ApplicationName,
                // https://docs.aws.amazon.com/elasticbeanstalk/latest/platforms/platforms-supported.html#platforms-supported.dotnetlinux
                SolutionStackName = "64bit Amazon Linux 2 v2.2.4 running .NET Core",
                OptionSettings = optionSettingProperties.ToArray(),
                // This line is critical - reference the label created in this same stack
                VersionLabel = applicationVersion.Ref
            });

            var output = new CfnOutput(this, "EndpointURL", new CfnOutputProps
            {
                Value = $"http://{environment.AttrEndpointUrl}/"
            });

            
            dynamoDb.GrantReadData(role);
            
            return environment;
        }
    }
}
