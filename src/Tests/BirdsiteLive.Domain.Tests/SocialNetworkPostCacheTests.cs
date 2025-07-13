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
    public class SocialNetworkPostCacheTests
    {
        private readonly InstanceSettings _settings;

        #region Ctor
        public SocialNetworkPostCacheTests()
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
            var id = "vsoisdc";
            var message = "hello";
            var user = new ExtractedTweet()
            {
                Id = id,
                MessageContent = message
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
            
            service.BackfillPostCache(user);
            var u = await service.GetPost(id, [f, f]);

            #region Validations
            Assert.AreEqual(u.Id, id);
            Assert.AreEqual(u.MessageContent, message);
            Assert.AreEqual(counter, 0);

            #endregion
        }
        [TestMethod]
        public async Task SimpleTwitterUserFetch()
        {
            #region Stubs
            var id = "vsoisdc";
            var message = "hello";
            var user = new ExtractedTweet()
            {
                Id = id,
                MessageContent = message
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
            
            var u = await service.GetPost(id, [f, f]);

            #region Validations
            Assert.AreEqual(u.Id, id);
            Assert.AreEqual(counter, 1);

            #endregion
        }
        [TestMethod]
        public async Task TwitterUserFetch_FirstFails_NotFound()
        {
            #region Stubs
            var id = "vsoisdc";
            var message = "hello";
            var user = new ExtractedTweet()
            {
                Id = id,
                MessageContent = message
            };
            var counter = 0;
            var fFailed =
                () =>
                {
                    counter++;
                    throw new UserNotFoundException();
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
            
            var u = await service.GetPost(id, [fFailed, fFailed2, f]);

            #region Validations
            Assert.IsNull(u);
            Assert.AreEqual(counter, 1);

            #endregion
        }
        [TestMethod]
        public async Task TwitterUserFetch_FirstFails()
        {
            #region Stubs
            var id = "vsoisdc";
            var message = "hello";
            var user = new ExtractedTweet()
            {
                Id = id,
                MessageContent = message
            };
            var counter = 0;
            var fFailed =
                () =>
                {
                    counter++;
                    throw new RateLimitExceededException();
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
            
            var u = await service.GetPost(id, [fFailed, f]);

            #region Validations
            Assert.AreEqual(u.Id, id);
            Assert.AreEqual(counter, 2);

            #endregion
        }
        
        [TestMethod]
        public async Task UseCacheOnSecondCall()
        {
            #region Stubs
            var id = "vsoisdc";
            var message = "hello";
            var user = new ExtractedTweet()
            {
                Id = id,
                MessageContent = message
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
            
            var up = service.GetPost(id, [f, f]);
            var u2p = service.GetPost(id, [f, f]);
            await Task.WhenAll(up, u2p);

            #region Validations
            Assert.AreEqual(up.Result.Id, id);
            Assert.AreEqual(u2p.Result.Id, id);
            Assert.AreEqual(counter, 1);

            #endregion
        }

    }
}
