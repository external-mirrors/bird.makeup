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
        void ExtractedDescription(int mentionsCount);
        void ExtractedStatus(int mentionsCount);
        void RegisterNewPosts(SocialMediaPost[] post);
        void RegisterNewActivity(Activity activity);
    }

    public class StatisticsHandler : IStatisticsHandler
    {
        static Meter _meter = new("DotMakeup", "1.0.0");
        static Counter<int> _activityCounter = _meter.CreateCounter<int>("dotmakeup_ap_activity");

        private static int _descriptionMentionsExtracted;
        private static int _statusMentionsExtracted;

        private static System.Timers.Timer _resetTimer;

        #region Ctor
        public StatisticsHandler()
        {
            if (_resetTimer == null)
            {
                _resetTimer = new System.Timers.Timer();
                _resetTimer.Elapsed += OnTimeResetEvent;
                _resetTimer.Interval = 24 * 60 * 60 * 1000; // 24h
                _resetTimer.Enabled = true;
            }
        }
        #endregion

        private void OnTimeResetEvent(object sender, ElapsedEventArgs e)
        {
            // Reset
            Interlocked.Exchange(ref _descriptionMentionsExtracted, 0);
            Interlocked.Exchange(ref _statusMentionsExtracted, 0);
        }

        public void ExtractedDescription(int mentionsCount)
        {
            for (var i = 0; i < mentionsCount; i++)
                Interlocked.Increment(ref _descriptionMentionsExtracted);
        }

        public void ExtractedStatus(int mentionsCount)
        {
            for (var i = 0; i < mentionsCount; i++)
                Interlocked.Increment(ref _statusMentionsExtracted);
        }

        public void RegisterNewPosts(SocialMediaPost[] post)
        {
        }

        public void RegisterNewActivity(Activity activity)
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
                case "Create":
                    _activityCounter.Add(1, new KeyValuePair<string, object>("type", "Like"));
                    break;

            }
        }
    }
}