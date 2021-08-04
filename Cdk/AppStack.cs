using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
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
                Code = Code.FromAsset($"{nameof(ProcessingLambda)}/bin/{buildConfiguration}/netcoreapp3.1/publish"),

                // Assembly::Type::Method
                Handler = $"{new ProcessingLambda.Function().GetType().Assembly.GetName().Name}::{nameof(ProcessingLambda.Function)}::{nameof(ProcessingLambda.Function.FunctionHandler)}"
            });

            // Setup Permissions
            ingestionQueue.GrantConsumeMessages(processingLambda);

            itemsTable.GrantWriteData(processingLambda);

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

            return application;

            /*
            var optionSettingProperties = new List<CfnEnvironment.OptionSettingProperty> {
                   new CfnEnvironment.OptionSettingProperty {
                        Namespace = "aws:autoscaling:launchconfiguration",
                        OptionName =  "IamInstanceProfile",
                        Value = instanceProfile.AttrArn
                   },
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
                   }
                };

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
            }

            var environment = new CfnEnvironment(this, "Environment", new CfnEnvironmentProps
            {
                EnvironmentName = settings.EnvironmentName,
                ApplicationName = settings.BeanstalkApplication.ApplicationName,
                PlatformArn = settings.ElasticBeanstalkPlatformArn,
                OptionSettings = optionSettingProperties.ToArray(),
                // This line is critical - reference the label created in this same stack
                VersionLabel = applicationVersion.Ref,
            });

            var output = new CfnOutput(this, "EndpointURL", new CfnOutputProps
            {
                Value = $"http://{environment.AttrEndpointUrl}/"
            });*/
        }
    }
}
