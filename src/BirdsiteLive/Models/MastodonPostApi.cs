using System;
using System.Collections.Generic;

namespace BirdsiteLive.Models;

public class MastodonPostApi
{
    public long id { get; set; }
    public string created_at { get; set; } = null!;
    public long? in_reply_to_id { get; set; } = null;
    public long? in_reply_to_account_id { get; set; } = null;
    public bool sensitive { get; set; } = false;
    public string spoiler_text { get; set; } = "";
    public string visibility { get; set; } = "public";
    public string language { get; set; } = "en";
    public string uri { get; set; }  = null!;
    public string url { get; set; }  = null!;
    public int replies_count { get; set; } = 0;
    public int reblogs_count { get; set; } = 0;
    public int favorite_count { get; set; } = 0;
    public string content { get; set; } = null!;
    public MastodonUserApi account { get; set; } = null!;
    public MastodonAppApi application { get; } = new MastodonAppApi();
    
    public List<MastodonAppApi> media_attachments { get; set; } = new List<MastodonAppApi>();
    public List<MastodonAppApi> mentions { get; set; } = new List<MastodonAppApi>();
    public List<MastodonAppApi> tags { get; set; } = new List<MastodonAppApi>();
    public List<MastodonAppApi> emojis { get; set; } = new List<MastodonAppApi>();
    public string card { get; set; } = null!;
    public string poll { get; set; } = null!;
    public string reblog { get; set; } = null!;
}
public class MastodonUserApi
{
    public long id { get; set; }
    public string username { get; set; } = null!;
    public string acct { get; set; } = null!;
    public string display_name { get; set; } = null!;
    public bool locked { get; set; } = false;
    public bool bot { get; set; } = true;
    public bool group { get; set; } = false;
    public string note { get; set; } = null!;
    public string url { get; set; } = null!;
    public string avatar { get; set; } = null!;
    public string avatar_static { get; set; } = null!;
    public string header { get; set; } = null!;
    public string header_static { get; set; } = null!;
    public int followers_count { get; set; } = 0;
    public int following_count { get; set; } = 0;
    public int statuses_count { get; set; } = 0;

    public List<MastodonAppApi> fields { get; set; } = new List<MastodonAppApi>();
    public List<MastodonAppApi> emojis { get; set; } = new List<MastodonAppApi>();
}

public class MastodonAppApi
{
    public string name { get; set; } = "bird.makeup";
    public string url { get; set; } = "https://bird.makeup/";
}
