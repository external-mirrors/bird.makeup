using System.Collections.Generic;
using System.Threading;
using System.Timers;
using BirdsiteLive.ActivityPub;
using BirdsiteLive.Common.Interfaces;
using System.Diagnostics.Metrics;
using BirdsiteLive.ActivityPub.Models;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Domain.Statistics
{
    public interface IStatisticsHandler
    {
        void RegisterNewPosts(SocialMediaPost[] post);
        void RegisterNewInboundActivity(Activity activity);
    }

    public class StatisticsHandler : IStatisticsHandler
    {
        static Meter _meter = new("DotMakeup", "1.0.0");
        static Counter<int> _activityCounter = _meter.CreateCounter<int>("dotmakeup_ap_activity");
        static Counter<int> _postsCounter = _meter.CreateCounter<int>("dotmakeup_new_posts_count");
        
        private readonly ISocialMediaService _socialMediaService;

        #region Ctor
        public StatisticsHandler(ISocialMediaService socialMediaService)
        {
            _socialMediaService = socialMediaService;
        }
        #endregion


        public void RegisterNewPosts(SocialMediaPost[] post)
        {
            _postsCounter.Add(post.Length, 
                    new KeyValuePair<string, object>("network", _socialMediaService.ServiceName));
        }

        public void RegisterNewInboundActivity(Activity activity)
        {
            switch (activity?.type)
            {
                case "Follow":
                    {
                        _activityCounter.Add(1, new KeyValuePair<string, object>("type", "Follow"),
                            new KeyValuePair<string, object>("network", _socialMediaService.ServiceName));
                        break;
                    }
                case "Delete":
                    {
                        _activityCounter.Add(1, new KeyValuePair<string, object>("type", "Delete"),
                            new KeyValuePair<string, object>("network", _socialMediaService.ServiceName));
                        break;
                    }
                case "Announce":
                    _activityCounter.Add(1, new KeyValuePair<string, object>("type", "Announce"),
                            new KeyValuePair<string, object>("network", _socialMediaService.ServiceName));
                    break;
                case "Like":
                    _activityCounter.Add(1, new KeyValuePair<string, object>("type", "Like"),
                            new KeyValuePair<string, object>("network", _socialMediaService.ServiceName));
                    break;
                case "Block":
                    _activityCounter.Add(1, new KeyValuePair<string, object>("type", "Block"),
                            new KeyValuePair<string, object>("network", _socialMediaService.ServiceName));
                    break;
                case "Create":
                    _activityCounter.Add(1, new KeyValuePair<string, object>("type", "Create"),
                            new KeyValuePair<string, object>("network", _socialMediaService.ServiceName));
                    break;

            }
        }
    }
}