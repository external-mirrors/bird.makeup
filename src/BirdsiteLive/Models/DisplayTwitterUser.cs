namespace BirdsiteLive.Models
{
    public class DisplayTwitterUser
    {
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Acct { get; set; } = null!;
        public string Url { get; set; } = null!;
        public string ProfileImageUrl { get; set; } = null!;
        public bool Protected { get; set; }
        public int FollowerCount { get; set; }
        public string MostPopularServer { get; set; } = null!;

        public string InstanceHandle { get; set; } = null!;
        public string FediverseAccount { get; set; } = null!;
        public string ServiceName { get; set; } = null!;
    }
}
