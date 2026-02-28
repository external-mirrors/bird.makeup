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
    public class ProcessUnfollowUserTests
    {
        [TestMethod]
        public async Task ExecuteAsync_NoFollowerFound_Test()
        {
            #region Stubs
            var username = "testest";
            var domain = "m.s";
            var twitterName = "handle";
            #endregion

            #region Mocks
            var followersDalMock = new Mock<IFollowersDal>(MockBehavior.Strict);
            followersDalMock
                .Setup(x => x.GetFollowerAsync(username, domain))
                .ReturnsAsync((Follower) null);

            var twitterUserDalMock = new Mock<ISocialMediaService>(MockBehavior.Strict);
            #endregion

            var action = new ProcessUndoFollowUser(followersDalMock.Object, twitterUserDalMock.Object);
            await action.ExecuteAsync(username, domain, twitterName );

            #region Validations
            followersDalMock.VerifyAll();
            twitterUserDalMock.VerifyAll();
            #endregion
        }

        [TestMethod]
        public async Task ExecuteAsync_NoTwitterUserFound_Test()
        {
            #region Stubs
            var username = "testest";
            var domain = "m.s";
            var twitterName = "handle";

            var follower = new Follower
            {
                Id = 1,
                Acct = username,
                Host = domain,
                Followings = new List<int>(),
            };
            #endregion

            #region Mocks
            var followersDalMock = new Mock<IFollowersDal>(MockBehavior.Strict);
            followersDalMock
                .Setup(x => x.GetFollowerAsync(username, domain))
                .ReturnsAsync(follower);

            var twitterUserDalMock = new Mock<ISocialMediaService>(MockBehavior.Strict);
            twitterUserDalMock
                .Setup(x => x.UserDal.GetUserAsync(twitterName))
                .ReturnsAsync((SyncUser)null);
            #endregion

            var action = new ProcessUndoFollowUser(followersDalMock.Object, twitterUserDalMock.Object);
            await action.ExecuteAsync(username, domain, twitterName);

            #region Validations
            followersDalMock.VerifyAll();
            twitterUserDalMock.VerifyAll();
            #endregion
        }

        [TestMethod]
        public async Task ExecuteAsync_MultiFollows_Test()
        {
            #region Stubs
            var username = "testest";
            var domain = "m.s";
            var twitterName = "handle";

            var follower = new Follower
            {
                Id = 1,
                Acct = username,
                Host = domain,
                Followings = new List<int> { 2, 3 },
                TotalFollowings = 2,
            };

            var twitterUser = new SyncUser
            {
                Id = 2,
                Acct = twitterName,
                LastTweetPostedId = 460,
            };

            var followerList = new List<Follower>
            {
                new Follower(),
                new Follower()
            };
            #endregion

            #region Mocks
            var followersDalMock = new Mock<IFollowersDal>(MockBehavior.Strict);
            followersDalMock
                .Setup(x => x.GetFollowerAsync(username, domain))
                .ReturnsAsync(follower);

            var twitterUserDalMock = new Mock<ISocialMediaService>(MockBehavior.Strict);
            twitterUserDalMock
                .Setup(x => x.UserDal.GetUserAsync(twitterName))
                .ReturnsAsync(twitterUser);
            
            twitterUserDalMock
                .Setup(x => x.UserDal.RemoveFollower(
                    It.Is<int>(y => y == 1),
                    It.Is<int>(y => y == 2)
                    ))
                .Returns(Task.CompletedTask);
            
            twitterUserDalMock
                .Setup(x => x.UserDal.GetFollowersCountAsync(
                    It.Is<int>(y => y == 2)
                    ))
                .ReturnsAsync(1);
            #endregion

            var action = new ProcessUndoFollowUser(followersDalMock.Object, twitterUserDalMock.Object);
            await action.ExecuteAsync(username, domain, twitterName);

            #region Validations
            followersDalMock.VerifyAll();
            twitterUserDalMock.VerifyAll();
            #endregion
        }

        [TestMethod]
        public async Task ExecuteAsync_CleanUp_Test()
        {
            #region Stubs
            var username = "testest";
            var domain = "m.s";
            var twitterName = "handle";

            var follower = new Follower
            {
                Id = 1,
                Acct = username,
                Host = domain,
                Followings = new List<int> { 2 },
            };

            SyncUser twitterUser = new SyncUser
            {
                Id = 2,
                Acct = twitterName,
                LastTweetPostedId = 460,
            };

            var followerList = new List<Follower>();
            #endregion

            #region Mocks
            var followersDalMock = new Mock<IFollowersDal>(MockBehavior.Strict);
            followersDalMock
                .Setup(x => x.GetFollowerAsync(username, domain))
                .ReturnsAsync(follower);

            followersDalMock
                .Setup(x => x.DeleteFollowerAsync(
                    It.Is<string>(y => y == username),
                    It.Is<string>(y => y == domain)
                    ))
                .Returns(Task.CompletedTask);
            

            var twitterUserDalMock = new Mock<ISocialMediaService>(MockBehavior.Strict);
            twitterUserDalMock
                .Setup(x => x.UserDal.GetUserAsync(twitterName))
                .ReturnsAsync(twitterUser);

            twitterUserDalMock
                .Setup(x => x.UserDal.DeleteUserAsync(
                    It.Is<string>(y => y == twitterName)
                ))
                .Returns(Task.CompletedTask);
            
            twitterUserDalMock
                .Setup(x => x.UserDal.RemoveFollower(
                    It.Is<int>(y => y == 1),
                    It.Is<int>(y => y == 2)
                    ))
                .Returns(Task.CompletedTask);
            
            twitterUserDalMock
                .Setup(x => x.UserDal.GetFollowersCountAsync(
                    It.Is<int>(y => y == 2)
                    ))
                .ReturnsAsync(0);
            #endregion

            var action = new ProcessUndoFollowUser(followersDalMock.Object, twitterUserDalMock.Object);
            await action.ExecuteAsync(username, domain, twitterName);

            #region Validations
            followersDalMock.VerifyAll();
            twitterUserDalMock.VerifyAll();
            #endregion
        }
    }
}