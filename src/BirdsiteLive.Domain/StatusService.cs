using System;
using System.Collections.Generic;
using System.Linq;
using BirdsiteLive.ActivityPub;
using BirdsiteLive.ActivityPub.Converters;
using BirdsiteLive.ActivityPub.Models;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Domain.Statistics;
using BirdsiteLive.Domain.Tools;

namespace BirdsiteLive.Domain
{
    public interface IStatusService
    {
        Note GetStatus(string username, SocialMediaPost post);
        ActivityCreateNote GetActivity(string username, SocialMediaPost tweet);
    }

    public class StatusService : IStatusService
    {
        private readonly InstanceSettings _instanceSettings;
        private readonly IStatusExtractor _statusExtractor;
        private readonly IExtractionStatisticsHandler _statisticsHandler;

        #region Ctor
        public StatusService(InstanceSettings instanceSettings, IStatusExtractor statusExtractor, IExtractionStatisticsHandler statisticsHandler)
        {
            _instanceSettings = instanceSettings;
            _statusExtractor = statusExtractor;
            _statisticsHandler = statisticsHandler;
        }
        #endregion

        public Note GetStatus(string username, SocialMediaPost post)
        {
            var actorUrl = UrlFactory.GetActorUrl(_instanceSettings.Domain, username);
            var noteUrl = UrlFactory.GetNoteUrl(_instanceSettings.Domain, username, post.Id.ToString());
            String announceId = null;
            if (post.IsRetweet)
            {
                actorUrl = UrlFactory.GetActorUrl(_instanceSettings.Domain, post.OriginalAuthor.Acct);
                noteUrl = UrlFactory.GetNoteUrl(_instanceSettings.Domain, post.OriginalAuthor.Acct, post.RetweetId.ToString());
                announceId  = UrlFactory.GetNoteUrl(_instanceSettings.Domain, username, post.Id.ToString());
            }

            var to = $"{actorUrl}/followers";

            var cc = new string[0];
            
            string summary = null;

            var extractedTags = _statusExtractor.Extract(post.MessageContent, _instanceSettings.ResolveMentionsInPosts);
            _statisticsHandler.ExtractedStatus(extractedTags.tags.Count(x => x.type == "Mention"));

            // Replace RT by a link
            var content = extractedTags.content;
            if (post.IsRetweet)
            {
                // content = "RT: " + content;
                cc = new[] {"https://www.w3.org/ns/activitystreams#Public"};
            }
            cc = new[] {"https://www.w3.org/ns/activitystreams#Public"};

            string inReplyTo = null;
            if (post.InReplyToStatusId != default)
                inReplyTo = $"https://{_instanceSettings.Domain}/users/{post.InReplyToAccount.ToLowerInvariant()}/statuses/{post.InReplyToStatusId}";


            Note note;
            if (post.Poll is not null)
                note = new Question() { };
            else
                note = new Note { };

            note.id = noteUrl;
            note.announceId = announceId;
            note.published = post.CreatedAt.ToString("s") + "Z";
            note.url = noteUrl;
            note.attributedTo = actorUrl;
            note.inReplyTo = inReplyTo;
            note.to = new[] { to };
            note.cc = cc;
            note.sensitive = false;
            note.summary = summary;
            note.content = $"<p>{content}</p>";
            note.attachment = Convert(post.Media);
            note.tag = extractedTags.tags;

            if (note is Question)
            {
                long totalVotes = 0;
                var nowString = post.Poll.endTime.ToString("s") + "Z";
                var options = new List<QuestionAnswer>();
                foreach ((string First, long Second) in post.Poll.options)
                {
                    var o = new QuestionAnswer();
                    o.name = First;
                    o.replies = new Dictionary<string, object>()
                    {
                        ["type"] = "Collection",
                        ["totalItems"] = Second,
                    };
                    totalVotes += Second;
                    options.Add(o);
                }

                ((Question)note).votersCount = totalVotes;
                ((Question)note).endTime = nowString;
                if (post.Poll.endTime < DateTime.Now)
                    ((Question)note).closed = nowString;
                ((Question)note).answers = options.ToArray();
            }
            
            return note;
        }

        public ActivityCreateNote GetActivity(string username, SocialMediaPost tweet)
        {
            var note = GetStatus(username, tweet);
            var actor = UrlFactory.GetActorUrl(_instanceSettings.Domain, username);
            String noteUri;
            string activityType;
            if (tweet.IsRetweet) 
            {
                noteUri = UrlFactory.GetNoteUrl(_instanceSettings.Domain, username, tweet.Id.ToString());
                activityType = "Announce";
            } else
            {
                noteUri = UrlFactory.GetNoteUrl(_instanceSettings.Domain, username, tweet.Id.ToString());
                activityType = "Create";
            }

            var now = DateTime.UtcNow;
            var nowString = now.ToString("s") + "Z";

            var noteActivity = new ActivityCreateNote()
            {
                context = "https://www.w3.org/ns/activitystreams",
                id = $"{noteUri}/activity",
                type = activityType,
                actor = actor,
                published = nowString,

                to = new[] {$"{actor}/followers"},
                cc = note.cc,
                apObject = note
            };

            return noteActivity;
        }

        private Attachment[] Convert(ExtractedMedia[] media)
        {
            if(media == null) return new Attachment[0];
            return media.Select(x =>
            {
                return new Attachment
                {
                    type = "Document",
                    url = x.Url,
                    mediaType = x.MediaType,
                    name = x.AltText
                };
            }).ToArray();
        }
    }
}
