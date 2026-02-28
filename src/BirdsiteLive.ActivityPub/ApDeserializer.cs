#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8613, CS8618, CS8619, CS8620, CS8621, CS8625, CS8629, CS8631, CS8634
using System;
using BirdsiteLive.ActivityPub.Models;
using System.Text.Json;

namespace BirdsiteLive.ActivityPub
{
    public class ApDeserializer
    {
        public static Activity ProcessActivity(string json)
        {
            var activity = DeserializeRequired<Activity>(json);
            if (string.IsNullOrWhiteSpace(activity.type))
                throw new JsonException("Incoming activity is missing required 'type'.");

            return activity.type switch
            {
                "Follow" => DeserializeRequired<ActivityFollow>(json),
                "Like" => DeserializeRequired<ActivityLike>(json),
                "Flag" => DeserializeRequired<ActivityFlag>(json),
                "Announce" => DeserializeRequired<Activity>(json),
                "Create" => DeserializeRequired<ActivityCreateNote>(json),
                "Delete" => DeserializeRequired<ActivityDelete>(json),
                "Undo" => DeserializeUndoActivity(json),
                "Accept" => DeserializeAcceptActivity(json),
                _ => activity,
            };
        }

        private static Activity DeserializeUndoActivity(string json)
        {
            var undoActivity = DeserializeRequired<ActivityUndo>(json);
            if (undoActivity.apObject?.type == "Follow")
                return DeserializeRequired<ActivityUndoFollow>(json);

            return undoActivity;
        }

        private static Activity DeserializeAcceptActivity(string json)
        {
            var accept = DeserializeRequired<ActivityAccept>(json);
            if (accept.apObject?.type != "Follow")
                return accept;

            return new ActivityAcceptFollow
            {
                type = accept.type,
                id = accept.id,
                actor = accept.actor,
                context = accept.context,
                apObject = new ActivityFollow()
                {
                    id = accept.apObject.id,
                    type = accept.apObject.type,
                    actor = accept.apObject.actor,
                    context = accept.apObject.context?.ToString(),
                    apObject = accept.apObject.apObject,
                }
            };
        }

        private static T DeserializeRequired<T>(string json) where T : class
        {
            return JsonSerializer.Deserialize<T>(json)
                   ?? throw new JsonException($"Unable to deserialize payload to {typeof(T).Name}.");
        }
    }
}
