namespace CloudAutoGroup.TVCampaign.Shared
{
    public class QuoteRequest
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string CarType { get; set; }
        public int CreditScoreEstimate { get; set; }
    }
}