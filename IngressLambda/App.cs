using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Shared;
using Shared.Ingestion;

namespace IngressLambda
{
    public class IngestionModel
    {
        public int CustomerId { get; set; }
        public string ItemData { get; set; }
    }

    public class App
    {
        private readonly IIngestionQueueClient _queue;

        public App(IIngestionQueueClient queue)
        {
            _queue = queue;
        }

        public async Task<APIGatewayProxyResponse> Get(APIGatewayProxyRequest request, ILambdaContext context)
        {
            // TODO verify logging
            // TODO: also log to cloud watch?  https://docs.aws.amazon.com/lambda/latest/dg/csharp-logging.html#csharp-logging-cwconsole
            context.Logger.LogLine($"[{GetType().FullName}] Get Request: {request.Body}");

            IngestionModel model = null;
            APIGatewayProxyResponse response;
            try
            {
                model = JsonConvert.DeserializeObject<IngestionModel>(request.Body);

                await QueueIngestionRequest(model);

                response = new APIGatewayProxyResponse
                {
                    StatusCode = (int) HttpStatusCode.OK,
                    Body = $"Queued Processing for Customer [{model?.CustomerId}]"
                };
            }
            catch (Exception e)
            {
                var errorJson = JsonConvert.SerializeObject(e);
                response = new APIGatewayProxyResponse
                {
                    StatusCode = (int) HttpStatusCode.BadRequest,
                    Body = $"Exception Ingesting Message for Customer [{model?.CustomerId}]: {errorJson}"
                };
            }
            
            response.Headers = new Dictionary<string, string>
            {
                {"Content-Type", "text/plain"},
                // add CORS headers
                {"Access-Control-Allow-Headers", "Content-Type"},
                {"Access-Control-Allow-Origin", "*"},
                {"Access-Control-Allow-Methods", "OPTIONS,POST,GET"}
            };

            context.Logger.LogLine($"Get Response: {response.Body}");
            
            return response;
        }

        private async Task QueueIngestionRequest(IngestionModel model)
        {
            // simulate performing some business logic.

            if (model.CustomerId <= 0)
                throw new ArgumentException("Invalid Customer Id");

            await _queue.Write(new IngestionMessage
            {
                CustomerId = model.CustomerId,
                Item = new Item
                {
                    Data = model.ItemData
                }
            });
        }
    }
}