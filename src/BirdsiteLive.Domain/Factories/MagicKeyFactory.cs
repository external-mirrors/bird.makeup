using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using BirdsiteLive.Cryptography;
using BirdsiteLive.DAL.Contracts;

namespace BirdsiteLive.Domain.Factories
{
    public interface IMagicKeyFactory
    {
        Task<MagicKey> GetMagicKey();
    }

    public class MagicKeyFactory : IMagicKeyFactory
    {
        private static MagicKey _magicKey = null!;
        private ISettingsDal _settings;

        #region Ctor
        public MagicKeyFactory(ISettingsDal settings)
        {
            _settings = settings;
        }
        #endregion

        public async Task<MagicKey> GetMagicKey()
        {
            //Cached key
            if (_magicKey != null) return _magicKey;

            var keyJson = await _settings.Get("key.json");
            
            //Generate key if needed
            if (keyJson is null)
            {
                var key = MagicKey.Generate();
                keyJson = JsonDocument.Parse(key.PrivateKey).RootElement;
                await _settings.Set("key.json", keyJson);
            }

            //Load and return key
            var serializedKey = keyJson.ToString();
            _magicKey = new MagicKey(serializedKey!);
            return _magicKey;
        }
    }
}
