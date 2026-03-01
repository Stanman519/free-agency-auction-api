using System.Collections.Generic;

namespace FreeAgencyAuctionAPI
{
    public class AppConfig
    {
        public AzureMessageQueue AzureMessageQueue { get; set; }
        public string SqlServerConnectionString { get; set; }
        public BingImageApi BingImageApi { get; set; }
        public MflKeys Mfl { get; set; }
        public StreamClient StreamClient { get; set; }
        public ApplicationInsights ApplicationInsights { get; set; }
        public SportsDataConfig SportsDataConfig { get; set; }
    }
    public class SportsDataConfig
    {
        public string SportsDataApiKey { get; set; }
    }
    public class ApplicationInsights
    {
        public string InstrumentationKey { get; set; }
        public string ConnectionString { get; set; }
    }
    public class AzureMessageQueue
    {
        public string AzureStorageConnectionString { get; set; }
    }
    public class BingImageApi
    {
        public string BingSubscriptionKey { get; set; }
    }
    public class MflKeys
    {
        public string CommishCookie { get; set; }
        public List<MflApiKey> MflApiKey { get; set; }
    }
    public class MflApiKey
    {
        public int id { get; set; }
        public string key { get; set; }
    }
    public class StreamClient
    {
        public string StreamKey { get; set; }
        public string StreamPassword { get; set; }
    }
    public class Auth0Config
    {
        public string Domain { get; set; }
        public string Audience { get; set; }
    }
}
