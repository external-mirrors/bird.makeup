using BirdsiteLive.Common.Settings;
using Ipfs.Http;

namespace dotMakeup.ipfs;

public interface IIpfsService
{
    string GetIpfsPublicLink(string hash);
    Task<string> Mirror(string upstream, bool pin);
    
}
public class DotmakeupIpfs : IIpfsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private InstanceSettings _instanceSettings;
    private readonly IpfsClient _ipfs;
    #region Ctor
    public DotmakeupIpfs(InstanceSettings instanceSettings, IHttpClientFactory httpClientFactory)
    {
        _instanceSettings = instanceSettings;
        _httpClientFactory = httpClientFactory;
        _ipfs = new IpfsClient();
        if (_instanceSettings.IpfsApi is not null)
            _ipfs.ApiUri = new Uri(_instanceSettings.IpfsApi);

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
}
