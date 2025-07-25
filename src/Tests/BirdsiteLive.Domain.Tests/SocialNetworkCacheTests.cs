using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BirdsiteLive.Common.Exceptions;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Domain.Tools;
using BirdsiteLive.Twitter.Models;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace BirdsiteLive.Domain.Tests
{
    [TestClass]
    public class SocialNetworkCacheTests
    {
        private readonly InstanceSettings _settings;

        #region Ctor
        public SocialNetworkCacheTests()
        {
            _settings = new InstanceSettings
            {
                Domain = "domain.name"
            };
        }
        #endregion

        [TestMethod]
        public async Task Backfill()
        {
            #region Stubs
            var username = "MyUserName";
            TwitterUser user = new TwitterUser()
            {
                Id = 12,
                Acct = username,
            };
            var counter = 0;
            var f =
                () =>
                {
                    counter++; 
                    return Task.FromResult(user);
                };
            #endregion

            var service = new SocialNetworkCache(_settings);
            
            service.BackfillUserCache(user);
            var u = await service.GetUser(username, [f, f]);

            #region Validations
            Assert.AreEqual(u.Acct, username);
            Assert.AreEqual(counter, 0);

            #endregion
        }
        [TestMethod]
        public async Task SimpleTwitterUserFetch()
        {
            #region Stubs
            var username = "MyUserName";
            TwitterUser user = new TwitterUser()
            {
                Id = 12,
                Acct = username,
            };
            var counter = 0;
            var f =
                () =>
                {
                    counter++; 
                    return Task.FromResult(user);
                };
            #endregion

            var service = new SocialNetworkCache(_settings);
            
            var u = await service.GetUser(username, [f, f]);

            #region Validations
            Assert.AreEqual(u.Acct, username);
            Assert.AreEqual(counter, 1);

            #endregion
        }
        [TestMethod]
        public async Task TwitterUserFetch_FirstFails_Null()
        {
            #region Stubs
            var username = "MyUserName";
            TwitterUser user = new TwitterUser()
            {
                Id = 12,
                Acct = username,
            };
            var counter = 0;
            var fFailed =
                () =>
                {
                    counter++;
                    return Task.FromResult((TwitterUser)null);
                };
            var f =
                () =>
                {
                    counter++; 
                    return Task.FromResult(user);
                };
            #endregion

            var service = new SocialNetworkCache(_settings);
            
            var u = await service.GetUser(username, [fFailed, f]);

            #region Validations
            Assert.AreEqual(u.Acct, username);
            Assert.AreEqual(counter, 2);

            #endregion
        }
        [TestMethod]
        [ExpectedException(typeof(UserNotFoundException))]
        public async Task TwitterUserFetch_FirstFails_NotFound()
        {
            #region Stubs
            var username = "MyUserName";
            TwitterUser user = new TwitterUser()
            {
                Id = 12,
                Acct = username,
            };
            var counter = 0;
            var fFailed =
                () =>
                {
                    counter++;
                    throw new UserNotFoundException();
                    return Task.FromResult(user);
                };
            var f =
                () =>
                {
                    counter++; 
                    return Task.FromResult(user);
                };
            #endregion

            var service = new SocialNetworkCache(_settings);
            
            var u = await service.GetUser(username, [fFailed, f]);

            #region Validations
            Assert.IsNull(u);
            Assert.AreEqual(counter, 1);

            #endregion
        }
        [TestMethod]
        public async Task TwitterUserFetch_FirstsFails()
        {
            #region Stubs
            var username = "MyUserName";
            TwitterUser user = new TwitterUser()
            {
                Id = 12,
                Acct = username,
            };
            var counter = 0;
            var fFailed =
                () =>
                {
                    counter++;
                    throw new RateLimitExceededException();
                    return Task.FromResult(user);
                };
            var fFailed2 =
                () =>
                {
                    counter++;
                    throw new HttpRequestException();
                    return Task.FromResult(user);
                };
            var f =
                () =>
                {
                    counter++; 
                    return Task.FromResult(user);
                };
            #endregion

            var service = new SocialNetworkCache(_settings);
            
            var u = await service.GetUser(username, [fFailed, fFailed2, f]);

            #region Validations
            Assert.AreEqual(u.Acct, username);
            Assert.AreEqual(counter, 3);

            #endregion
        }
        
        [TestMethod]
        public async Task UseCacheOnSecondCall()
        {
            #region Stubs
            var username = "MyUserName";
            TwitterUser user = new TwitterUser()
            {
                Id = 12,
                Acct = username,
            };
            var counter = 0;
            var f =
                () =>
                {
                    counter++; 
                    return Task.FromResult(user);
                };
            #endregion

            var service = new SocialNetworkCache(_settings);
            
            var up = service.GetUser(username, [f, f]);
            var u2p = service.GetUser(username, [f, f]);
            await Task.WhenAll(up, u2p);

            #region Validations
            Assert.AreEqual(up.Result.Acct, username);
            Assert.AreEqual(u2p.Result.Acct, username);
            Assert.AreEqual(counter, 1);

            #endregion
        }

    }
}
