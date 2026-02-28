#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8613, CS8618, CS8619, CS8620, CS8621, CS8625, CS8629, CS8631, CS8634
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        Task<Note> GetStatus(string username, SocialMediaPost post);
        Task<ActivityCreateNote> GetActivity(string username, SocialMediaPost tweet);
    }

    public class StatusService : IStatusService
    {
        private readonly InstanceSettings _instanceSettings;
        private readonly IStatusExtractor _statusExtractor;

        #region Ctor
        public StatusService(InstanceSettings instanceSettings, IStatusExtractor statusExtractor)
        {
            _instanceSettings = instanceSettings;
            _statusExtractor = statusExtractor;
        }
        #endregion

        public async Task<Note> GetStatus(string username, SocialMediaPost post)
        {
            var actorUrl = UrlFactory.GetActorUrl(_instanceSettings.Domain, username);
            var noteUrl = UrlFactory.GetNoteUrl(_instanceSettings.Domain, username, post.Id.ToString());
            string? announceId = null;
            if (post.IsRetweet)
            {
                actorUrl = UrlFactory.GetActorUrl(_instanceSettings.Domain, post.OriginalAuthor.Acct);
                noteUrl = UrlFactory.GetNoteUrl(_instanceSettings.Domain, post.OriginalAuthor.Acct, post.RetweetId.ToString());
                announceId  = UrlFactory.GetNoteUrl(_instanceSettings.Domain, username, post.Id.ToString());
            }

            var to = $"{actorUrl}/followers";

            var cc = new string[0];
            
            string? summary = null;

            var extractedTags = await _statusExtractor.Extract(post.MessageContent, _instanceSettings.ResolveMentionsInPosts);
            var tags = extractedTags.tags.ToList();

            var content = extractedTags.content;
            cc = new[] {"https://www.w3.org/ns/activitystreams#Public"};

            string? inReplyTo = null;
            if (post.InReplyToStatusId != default)
                inReplyTo = $"https://{_instanceSettings.Domain}/users/{post.InReplyToAccount.ToLowerInvariant()}/statuses/{post.InReplyToStatusId}";


            Note note;
            if (post.Poll is not null)
                note = new Question() { };
            else
                note = new Note { };

            if (post.QuotedAccount is not null && post.QuotedStatusId != null)
            {
                var quoteId =
                    $"https://{_instanceSettings.Domain}/users/{post.QuotedAccount}/statuses/{post.QuotedStatusId}";
                content += "<span class=\"quote-inline\"><br/><a href=\"" + quoteId + "\">" + quoteId + "</a></span>";
                tags.Add(new Tag()
                {
                    href = quoteId,
                    name = quoteId,
                    type = "Link",
                    rel = "https://misskey-hub.net/ns#_misskey_quote",
                    mediaType = "application/ld+json; profile=\"https://www.w3.org/ns/activitystreams\"",
                });
                note.quoteAuthorization = UrlFactory.GetStampUrl(_instanceSettings.Domain, post.QuotedAccount, post.QuotedStatusId, noteUrl);
                note.quote = quoteId;
                note.quoteUrl = quoteId;
            }
            
            note.id = noteUrl;
            note.announceId = announceId;
            note.published = post.CreatedAt.ToString("s") + "Z";
            note.url = noteUrl;
            note.attributedTo = actorUrl;
            note.inReplyTo = inReplyTo;
            note.to = [ to ];
            note.cc = cc;
            note.sensitive = false;
            note.summary = summary;
            note.content = $"<p>{content}</p>";
            note.attachment = Convert(post.Media);
            note.tag = tags.ToArray();

            if (note is Question question && post.Poll is { } poll)
            {
                long totalVotes = 0;
                var nowString = poll.endTime.ToString("s") + "Z";
                var options = new List<QuestionAnswer>();
                foreach ((string First, long Second) in poll.options)
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

                question.votersCount = totalVotes;
                question.endTime = nowString;
                if (poll.endTime < DateTime.Now)
                    question.closed = nowString;
                question.answers = options.ToArray();
            }

            if (post.LikeCount != default)
            {
                note.likes = new Collection()
                {
                    id = $"{noteUrl}/likes",
                    totalItems = post.LikeCount,
                    context = null,
                };
            }
            if (post.ShareCount != default)
            {
                note.shares = new Collection()
                {
                    id = $"{noteUrl}/shares",
                    totalItems = post.ShareCount,
                    context = null,
                };
            }
            
            return note;
        }

        public async Task<ActivityCreateNote> GetActivity(string username, SocialMediaPost tweet)
        {
            var note = await GetStatus(username, tweet);
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

                to = [ $"{actor}/followers" ],
                cc = note.cc,
                apObject = note
            };

            return noteActivity;
        }

        private Attachment[] Convert(ExtractedMedia[]? media)
        {
            if (media is null)
                return Array.Empty<Attachment>();
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
