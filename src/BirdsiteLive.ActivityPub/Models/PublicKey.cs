namespace BirdsiteLive.ActivityPub
{
    public class PublicKey
    {
        public string id { get; set; } = null!;
        public string owner { get; set; } = null!;
        public string publicKeyPem { get; set; } = null!;
    }
}
