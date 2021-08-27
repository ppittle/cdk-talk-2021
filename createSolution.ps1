#pre-req AWS Project Templates (uncomment to install/upgrade)
dotnet tool install -g Amazon.Lambda.Tools
dotnet tool update -g Amazon.Lambda.Tools
dotnet new -i "Amazon.Lambda.Templates::*"

dotnet new sln --name CloudAutoGroup.TVCampaign

# Add Web project (ASP.NET Core Web - .NET 5)
dotnet new mvc --name CloudAutoGroup.TVCampaign.Web --output ./Web
dotnet sln ./CloudAutoGroup.TVCampaign.sln add ./Web

# Add Request Quote Api project (AWS Lambda Web Api - .NET 5)
dotnet new serverless.image.EmptyServerless --name CloudAutoGroup.TVCampaign.RequestQuoteApi --output ./
Move-Item -Path ./src/CloudAutoGroup.TVCampaign.RequestQuoteApi -Destination ./RequestQuoteApi
Remove-Item -Path "./src","./test" -Recurse -Force
dotnet sln ./CloudAutoGroup.TVCampaign.sln add ./RequestQuoteApi/

# Add Request Quote Processor project (AWS Lambda SQS - .NET Core 3.1)
dotnet new lambda.SQS --name CloudAutoGroup.TVCampaign.RequestQuoteProcessor --output ./
Move-Item -Path ./src/CloudAutoGroup.TVCampaign.RequestQuoteProcessor -Destination ./RequestQuoteProcessor
Remove-Item -Path "./src","./test" -Recurse -Force
dotnet sln ./CloudAutoGroup.TVCampaign.sln add ./RequestQuoteProcessor

# Add Shared Library (Class Library - .NET Core 3.1)
dotnet new classlib --framework netcoreapp3.1 --name CloudAutoGroup.TVCampaign.Shared --output ./Shared
dotnet sln ./CloudAutoGroup.TVCampaign.sln add ./Shared
# Add project references
dotnet add ./Web reference ./Shared
dotnet add ./RequestQuoteApi/ reference ./Shared
dotnet add ./RequestQuoteProcessor/ reference ./Shared

# Add Nuget Packages (Web)
dotnet add ./Web package "AWSSDK.Core" -v "3.7.2.4"
dotnet add ./Web package "AWSSDK.Extensions.NETCore.Setup" -v "3.7.1"
# Add Nuget Packages (Request Quote Api)
dotnet add ./RequestQuoteApi/ package "Microsoft.Extensions.Configuration" -v "5.0.0"
dotnet add ./RequestQuoteApi/ package "Microsoft.Extensions.Configuration.EnvironmentVariables" -v "5.0.0"
dotnet add ./RequestQuoteApi/ package "Microsoft.Extensions.DependencyInjection" -v "5.0.1"
dotnet add ./RequestQuoteApi/ package "Microsoft.Extensions.Options.ConfigurationExtensions" -v "5.0.0"
dotnet add ./RequestQuoteApi/ package "Newtonsoft.Json" -v "13.0.1"
# Add Nuget Packages (Request Quote Processor)
dotnet add ./RequestQuoteProcessor/ package "Microsoft.Extensions.Configuration" -v "5.0.0"
dotnet add ./RequestQuoteProcessor/ package "Microsoft.Extensions.Configuration.EnvironmentVariables" -v "5.0.0"
dotnet add ./RequestQuoteProcessor/ package "Microsoft.Extensions.DependencyInjection" -v "5.0.1"
dotnet add ./RequestQuoteProcessor/ package "Microsoft.Extensions.Options.ConfigurationExtensions" -v "5.0.0"
dotnet add ./RequestQuoteProcessor/ package "Newtonsoft.Json" -v "13.0.1"
# Add Nuget Packages (Shared Library)
dotnet add ./Shared package "AWSSDK.DynamoDBv2" -v "3.7.0.53"
dotnet add ./Shared package "AWSSDK.Extensions.NETCore.Setup" -v "3.7.1"
dotnet add ./Shared package "AWSSDK.SQS" -v "3.7.0.53"
dotnet add ./Shared package "AWSSDK.SimpleNotificationService" -v "3.7.2.23"
dotnet add ./Shared package "Microsoft.Extensions.DependencyInjection.Abstractions" -v "5.0.0"
dotnet add ./Shared package "Microsoft.Extensions.Options" -v "5.0.0"
dotnet add ./Shared package "Newtonsoft.Json" -v "13.0.1"
dotnet add ./Shared package "System.Linq.Async" -v "5.0.0"

# Build
dotnet build .