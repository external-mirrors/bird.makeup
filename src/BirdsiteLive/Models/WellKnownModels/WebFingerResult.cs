using System.Collections.Generic;

namespace BirdsiteLive.Models.WellKnownModels
{
    public class WebFingerResult
    {
        public string subject { get; set; } = null!;
        public string[] aliases { get; set; } = null!;
        public List<WebFingerLink> links { get; set; } = new List<WebFingerLink>();
    }
}
