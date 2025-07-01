using System;
using System.Text;

namespace BirdsiteLive.ActivityPub.Converters
{
    public class UrlFactory
    {
        public static string GetActorUrl(string domain, string username)
        {
            return $"https://{domain.ToLowerInvariant()}/users/{username.ToLowerInvariant()}";
        }

        public static string GetNoteUrl(string domain, string username, string noteId)
        {
            return $"https://{domain.ToLowerInvariant()}/users/{username.ToLowerInvariant()}/statuses/{noteId}";
        }
        public static string GetStampUrl(string domain, string username, string noteId, string remote)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(remote);
            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                hex.AppendFormat("{0:x2}", b); // lowercase hexadecimal
            }
            return $"https://{domain.ToLowerInvariant()}/users/{username.ToLowerInvariant()}/stamp/{noteId}/{hex}";
        }
        public static string DecodeStampRemoteUrl(string hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return Encoding.UTF8.GetString(bytes);
        }
    }
}