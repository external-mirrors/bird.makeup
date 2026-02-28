namespace BirdsiteLive.Models.WellKnownModels
{
    public class NodeInfoV21
    {
        public string version { get; set; } = null!;
        public string[] protocols { get; set; } = null!;
        public Usage usage { get; set; } = null!;
        public bool openRegistrations { get; set; }
        public SoftwareV21 software { get; set; } = null!;
        public Services services { get; set; } = null!;
        public Metadata metadata { get; set; } = null!;
    }
}
