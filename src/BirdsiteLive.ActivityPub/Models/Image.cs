namespace BirdsiteLive.ActivityPub
{
    public class Image
    {
        public string type { get; set; } = "Image";
        public string mediaType { get; set; } = null!;
        public string url { get; set; } = null!;
    }
}
