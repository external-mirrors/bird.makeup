using System.Collections.Generic;
using System.Threading;
using System.Timers;
using BirdsiteLive.ActivityPub;
using BirdsiteLive.Common.Interfaces;
using System.Diagnostics.Metrics;
using BirdsiteLive.ActivityPub.Models;

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

        #region Ctor
        public StatisticsHandler()
        {
        }
        #endregion


        public void RegisterNewPosts(SocialMediaPost[] post)
        {
        }

        public void RegisterNewInboundActivity(Activity activity)
        {
            switch (activity?.type)
            {
                case "Follow":
                    {
                        _activityCounter.Add(1, new KeyValuePair<string, object>("type", "Follow"));
                        break;
                    }
                case "Delete":
                    {
                        _activityCounter.Add(1, new KeyValuePair<string, object>("type", "Delete"));
                        break;
                    }
                case "Announce":
                    _activityCounter.Add(1, new KeyValuePair<string, object>("type", "Announce"));
                    break;
                case "Like":
                    _activityCounter.Add(1, new KeyValuePair<string, object>("type", "Like"));
                    break;
                case "Block":
                    _activityCounter.Add(1, new KeyValuePair<string, object>("type", "Block"));
                    break;
                case "Create":
                    _activityCounter.Add(1, new KeyValuePair<string, object>("type", "Create"));
                    break;

            }
        }
    }
}