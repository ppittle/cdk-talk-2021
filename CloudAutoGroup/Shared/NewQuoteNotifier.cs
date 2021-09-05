using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Microsoft.Extensions.Options;

namespace CloudAutoGroup.TVCampaign.Shared
{
    public interface INewQuoteNotifier
    {
        Task SendNewQuoteNotification();
    }

    public class NewQuoteNotifierSettings
    {
        public string  NotificationTopicArn { get; set; }
    }

    public class NewQuoteNotifier : INewQuoteNotifier
    {
        private readonly Amazon.SimpleNotificationService.IAmazonSimpleNotificationService _notifier;
        private readonly IOptions<NewQuoteNotifierSettings> _settings;

        public NewQuoteNotifier(IAmazonSimpleNotificationService notifier, IOptions<NewQuoteNotifierSettings> settings)
        {
            _notifier = notifier;
            _settings = settings;
        }

        public async Task SendNewQuoteNotification()
        {
            await _notifier.PublishAsync(_settings.Value.NotificationTopicArn, "New Quote Processed!");
        }
    }
}
