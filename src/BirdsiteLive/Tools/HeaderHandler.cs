#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8613, CS8618, CS8619, CS8620, CS8621, CS8625, CS8629, CS8631, CS8634
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace BirdsiteLive.Tools
{
    public class HeaderHandler
    {
        public static Dictionary<string, string> RequestHeaders(IHeaderDictionary header)
        {
            return header.ToDictionary<KeyValuePair<string, StringValues>, string, string>(h => h.Key.ToLowerInvariant(), h => h.Value)!;
        }
    }
}
