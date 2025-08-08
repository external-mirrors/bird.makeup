using System.Diagnostics.Metrics;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using Ipfs.Http;

namespace dotMakeup.ipfs;

public interface IIpfsService
{
    string GetIpfsPublicLink(string hash);
    Task<string> Mirror(string upstream, bool pin);
    Task<SocialMediaPost> Mirror(SocialMediaPost post, bool pin);
    Task Unpin(string hash);

    Task GarbageCollection();
    Task<string[]> AllPinnedHashes();
}
public class DotmakeupIpfs : IIpfsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private InstanceSettings _instanceSettings;
    private readonly IpfsClient _ipfs;
    static Meter _meter = new("DotMakeup", "1.0.0");
    private ObservableGauge<float> _diskUsageGauge; 
    private float _diskUsage = 0;
    #region Ctor
    public DotmakeupIpfs(InstanceSettings instanceSettings, IHttpClientFactory httpClientFactory)
    {
        _instanceSettings = instanceSettings;
        _httpClientFactory = httpClientFactory;
        _ipfs = new IpfsClient();
        if (_instanceSettings.IpfsApi is not null)
            _ipfs.ApiUri = new Uri(_instanceSettings.IpfsApi);

        _diskUsageGauge = _meter.CreateObservableGauge<float>("dotmakeup_ipfs_disk_usage", () => _diskUsage, "Gigabytes of disk usage" );
        Task.Run(UpdateStats);
    }
    #endregion

    public string GetIpfsPublicLink(string hash)
    {
        return $"https://{_instanceSettings.IpfsGateway}/ipfs/{hash}";
    }

    public async Task<string> Mirror(string upstream, bool pin)
    {
        var client = _httpClientFactory.CreateClient();
        var pic = await client.GetAsync(upstream);
        pic.EnsureSuccessStatusCode();
        var picData = await pic.Content.ReadAsByteArrayAsync();
        
        using var memoryStream = new MemoryStream(picData);
        
        var i = await _ipfs.FileSystem.AddAsync(memoryStream);
        
        if (pin)
            await _ipfs.Pin.AddAsync(i.Id);
        
        var gatewayClient = _httpClientFactory.CreateClient();
        gatewayClient.Timeout = TimeSpan.FromMinutes(3);
        try
        {
            await gatewayClient.GetAsync(GetIpfsPublicLink(i.Id));
        }
        catch (Exception e)
        {
            Console.WriteLine("Timeout during warmup of {0}", i.Id);
        }
        return i.Id;
    }

    public async Task<SocialMediaPost> Mirror(SocialMediaPost post, bool pin)
    {
        if (_instanceSettings.IpfsApi is not null) 
        {
            foreach (ExtractedMedia m in post.Media) {
                var hash = await Mirror(m.Url, pin);
                m.Url = GetIpfsPublicLink(hash);
            } 
        }

        return post;
    }

    public async Task Unpin(string hash)
    {
        await _ipfs.Pin.RemoveAsync(hash);
    }

    public async Task GarbageCollection()
    {
        await _ipfs.BlockRepository.RemoveGarbageAsync();
    }
    public async Task<string[]> AllPinnedHashes()
    {
        var l = await _ipfs.Pin.ListAsync();
        var hashes = l.Select(x => x.ToString());

        return hashes.ToArray();
    }

    private async Task UpdateStats()
    {
        while (true)
        {
            try
            {
                var a = await _ipfs.Stats.RepositoryAsync();
                if (a is null)
                    return;
                _diskUsage = a.RepoSize = a.RepoSize / 1024 / 1024 / 1024;
            }
            catch (Exception _)
            {
                // ignored
            }
            await Task.Delay(TimeSpan.FromMinutes(1));
        }
    }
}
