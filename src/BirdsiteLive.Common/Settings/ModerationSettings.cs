namespace BirdsiteLive.Common.Settings
{
    public class ModerationSettings
    {
        public string FollowersWhiteListing { get; set; } = null!;
        public string FollowersBlackListing { get; set; } = null!;
        public string TwitterAccountsWhiteListing { get; set; } = null!;
        public string TwitterAccountsBlackListing { get; set; } = null!;
    }
}
