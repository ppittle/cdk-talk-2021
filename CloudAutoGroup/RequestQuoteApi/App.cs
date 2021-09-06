using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using CloudAutoGroup.TVCampaign.Shared;
using Newtonsoft.Json;

namespace CloudAutoGroup.TVCampaign.RequestQuoteApi
{
    public class App
    {
        private readonly IQuoteRequestQueueClient _queue;

        public App(IQuoteRequestQueueClient queue)
        {
            _queue = queue;
        }

        public async Task<APIGatewayProxyResponse> Get(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine($"Get Request: {request.Body}\n");

            APIGatewayProxyResponse response;
            try
            {
                var quoteRequest = JsonConvert.DeserializeObject<QuoteRequest>(request.Body);

                await QueueIngestionRequest(quoteRequest);

                response = new APIGatewayProxyResponse
                {
                    StatusCode = (int) HttpStatusCode.OK,
                    Body = $"Queued Processing Quote Request for [{quoteRequest.Name}]"
                };
            }
            catch (Exception e)
            {
                var errorJson = JsonConvert.SerializeObject(e);
                response = new APIGatewayProxyResponse
                {
                    StatusCode = (int) HttpStatusCode.BadRequest,
                    Body = $"Exception Processing Quote Request: {errorJson}"
                };
            }

            response.Headers = new Dictionary<string, string>
            {
                {"Content-Type", "text/plain"}
            };

            return response;
        }

        private async Task QueueIngestionRequest(QuoteRequest request)
        {
            await _queue.Write(request);
        }
    }
}