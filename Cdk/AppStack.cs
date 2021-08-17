using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
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
using Amazon.CDK.AWS.S3.Assets;
using Amazon.CDK.AWS.SQS;
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
            //var queue = CreateQueue();

            var dynamoDb = CreateDataStore();

            //var ingestionLambda = CreateIngestionApiFunction(queue);

            //var processingLambda = CreateProcessingFunction(queue, dynamoDb);

            var website = CreateElasticBeanstalk(null, dynamoDb);
        }
        
        private Queue CreateQueue()
        {
            return new Amazon.CDK.AWS.SQS.Queue(this, "queue", new QueueProps
            {
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
                TableName = "items",
                BillingMode = BillingMode.PROVISIONED,
                ReadCapacity = 1,
                WriteCapacity = 1,
                RemovalPolicy = RemovalPolicy.DESTROY,
                PartitionKey = new Attribute
                {
                    Name = "id",
                    Type = AttributeType.STRING
                },
                SortKey = new Attribute
                {
                    Name = "createdAt",
                    Type = AttributeType.NUMBER
                }
            });

            var writeAutoScaling = table.AutoScaleWriteCapacity(new EnableScalingProps
            {
                MinCapacity = 1,
                MaxCapacity = 3
            });

            writeAutoScaling.ScaleOnUtilization(new UtilizationScalingProps{ TargetUtilizationPercent = 70 });

            return table;
        }

        /// <summary>
        /// Dotnet 5 Container Image Lambda + Api Gateway
        /// </summary>
        private Function CreateIngestionApiFunction(Queue ingestionQueue)
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
                Handler = $"{new ProcessingLambda.Function().GetType().Assembly.GetName().Name}::{nameof(ProcessingLambda.Function)}::{nameof(ProcessingLambda.Function.FunctionHandler)}"
            });

            // Setup Permissions
            ingestionQueue.GrantConsumeMessages(processingLambda);

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

        private CfnEnvironment CreateElasticBeanstalk(object ingestionLambda, object dynamoDb)
        {
            var currentDirectory = Directory.GetCurrentDirectory();

            var deployAsset = new Asset(this, "webDeploy", new AssetProps
            {
                // Assume we're running at root solution with `cdk deploy`
                Path = Path.Combine(Directory.GetCurrentDirectory(), @"Web\bin\Debug\net5.0\publish\")
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

            
            var optionSettingProperties = new List<CfnEnvironment.OptionSettingProperty> {
                   new CfnEnvironment.OptionSettingProperty {
                        Namespace = "aws:autoscaling:launchconfiguration",
                        OptionName =  "IamInstanceProfile",
                        Value = instanceProfile.AttrArn
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
                VersionLabel = applicationVersion.Ref,
            });

            var output = new CfnOutput(this, "EndpointURL", new CfnOutputProps
            {
                Value = $"http://{environment.AttrEndpointUrl}/"
            });

            return environment;
        }
    }
}
