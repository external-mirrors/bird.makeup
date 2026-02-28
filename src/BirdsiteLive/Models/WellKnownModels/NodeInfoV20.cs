using System.ComponentModel.DataAnnotations;

namespace BirdsiteLive.Models.WellKnownModels
{
    public class NodeInfoV20
    {
        public string version { get; set; } = null!;
        public string[] protocols { get; set; } = null!;
        public Software software { get; set; } = null!;
        public Usage usage { get; set; } = null!;
        public bool openRegistrations { get; set; }
        public Services services { get; set; } = null!;
        public Metadata metadata { get; set; } = null!;
    }
}
