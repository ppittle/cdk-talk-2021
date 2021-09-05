using System;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using CloudAutoGroup.TVCampaign.Shared;
using Newtonsoft.Json;

namespace CloudAutoGroup.TVCampaign.RequestQuoteProcessor
{
    public class App
    {
        private readonly IFullQuoteRepository _quoteRepository;
        private readonly INewQuoteNotifier _newQuoteNotifier;

        public App(IFullQuoteRepository quoteRepository, INewQuoteNotifier newQuoteNotifier)
        {
            _quoteRepository = quoteRepository;
            _newQuoteNotifier = newQuoteNotifier;
        }

        public async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            context.Logger.LogLine($"Processed message {message.Body}");

            try
            {
                var request = JsonConvert.DeserializeObject<QuoteRequest>(message.Body);

                var model = ProcessItemData(request);

                await _quoteRepository.Insert(model);

                await _newQuoteNotifier.SendNewQuoteNotification();

                // next step - email quote to customer

                context.Logger.LogLine($"[{GetType().FullName}] Processed message {message.Body}");
            }
            catch (Exception e)
            {
                context.Logger.LogLine($"[{GetType().FullName}] Error on message {message.Body}.  {JsonConvert.SerializeObject(e)}");
            }
        }

        private FullQuote ProcessItemData(QuoteRequest request)
        {
            // simulate quote generation
            var monthlyPremium = new Random().Next(60, 150);

            var fullQuote = new FullQuote
            {
                Request = request,
                MonthlyPremium = monthlyPremium
            };

            return fullQuote;
        }
    }
}