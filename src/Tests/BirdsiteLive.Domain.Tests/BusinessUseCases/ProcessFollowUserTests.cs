#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8613, CS8618, CS8619, CS8620, CS8621, CS8625, CS8629, CS8631, CS8634
using System.Collections.Generic;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Models;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.Domain.BusinessUseCases;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace BirdsiteLive.Domain.Tests.BusinessUseCases
{
    [TestClass]
    public class ProcessFollowUserTests
    {
        [TestMethod]
        public async Task ExecuteAsync_UserDontExists_TwitterDontExists_Test()
        {
            #region Stubs
            var username = "testest";
            var domain = "m.s";
            var twitterName = "handle";
            var followerInbox = "/user/testest";
            var inbox = "/inbox";
            var actorId = "actorUrl";

            var follower = new Follower
            {
                Id = 1,
                Acct = username,
                Host = domain,
                SharedInboxRoute = followerInbox,
                InboxRoute = inbox,
                Followings = new List<int>(),
            };

            var twitterUser = new SyncTwitterUser
            {
                Id = 2,
                Acct = twitterName,
                LastTweetPostedId = -1,
            };
            #endregion

            #region Mocks
            var followersDalMock = new Mock<IFollowersDal>();
            followersDalMock
                .SetupSequence(x => x.GetFollowerAsync(username, domain))
                .ReturnsAsync((Follower)null)
                .ReturnsAsync(follower);

            followersDalMock
                .Setup(x => x.CreateFollowerAsync(
                    It.Is<string>(y => y == username),
                    It.Is<string>(y => y == domain),
                    It.Is<string>(y => y == followerInbox),
                    It.Is<string>(y => y == inbox),
                    It.Is<string>(y => y == actorId),
                    null! ))
                .Returns(Task.CompletedTask);

            var twitterUserDalMock = new Mock<SocialMediaUserDal>();
            twitterUserDalMock
                .SetupSequence(x => x.GetUserAsync(twitterName))
                .ReturnsAsync((SyncTwitterUser)null!)
                .ReturnsAsync(twitterUser);

            twitterUserDalMock
                .Setup(x => x.CreateUserAsync(
                    It.Is<string>(y => y == twitterName)))
                .Returns(Task.CompletedTask);

            var socialMediaServiceMock = new Mock<ISocialMediaService>();
            socialMediaServiceMock.SetupGet(x => x.UserDal)
                .Returns(twitterUserDalMock.Object);
            #endregion

            var action = new ProcessFollowUser(followersDalMock.Object, socialMediaServiceMock.Object);
            await action.ExecuteAsync(username, domain, twitterName, followerInbox, inbox, actorId);

            #region Validations
            followersDalMock.VerifyAll();
            twitterUserDalMock.VerifyAll();
            #endregion
        }

        [TestMethod]
        public async Task ExecuteAsync_UserExists_TwitterExists_Test()
        {
            #region Stubs
            var username = "testest";
            var domain = "m.s";
            var twitterName = "handle";
            var followerInbox = "/user/testest";
            var inbox = "/inbox";
            var actorId = "actorUrl";
            
            var follower = new Follower
            {
                Id = 1,
                Acct = username,
                Host = domain,
                SharedInboxRoute = followerInbox,
                InboxRoute = inbox,
                Followings = new List<int>(),
            };

            var twitterUser = new SyncTwitterUser
            {
                Id = 2,
                Acct = twitterName,
                LastTweetPostedId = -1,
            };
            #endregion

            #region Mocks
            var followersDalMock = new Mock<IFollowersDal>();
            followersDalMock
                .Setup(x => x.GetFollowerAsync(username, domain))
                .ReturnsAsync(follower);
            
            var twitterUserDalMock = new Mock<SocialMediaUserDal>();
            twitterUserDalMock
                .Setup(x => x.GetUserAsync(twitterName))
                .ReturnsAsync(twitterUser);
            
            var socialMediaServiceMock = new Mock<ISocialMediaService>();
            socialMediaServiceMock.SetupGet(x => x.UserDal)
                .Returns(twitterUserDalMock.Object);
            #endregion

            var action = new ProcessFollowUser(followersDalMock.Object, socialMediaServiceMock.Object);
            await action.ExecuteAsync(username, domain, twitterName, followerInbox, inbox, actorId);

            #region Validations
            followersDalMock.VerifyAll();
            twitterUserDalMock.VerifyAll();
            #endregion
        }
    }
}
