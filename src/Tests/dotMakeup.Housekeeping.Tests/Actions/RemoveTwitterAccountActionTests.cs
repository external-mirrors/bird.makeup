using System.Collections.Generic;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Models;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.Moderation.Actions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace BirdsiteLive.Moderation.Tests.Actions
{
    [TestClass]
    public class RemoveTwitterAccountActionTests
    {
        [TestMethod]
        public async Task ProcessAsync_RemoveFollower()
        {
            #region Stubs
            var twitter = new SyncTwitterUser
            {
                Id = 24,
                Acct = "my-acct"
            };

            var followers = new List<Follower>
            {
                new Follower
                {
                    Id = 48,
                    Followings = new List<int>{ 24 },
                }
            };
            #endregion

            #region Mocks
            var followersDalMock = new Mock<IFollowersDal>();

            //followersDalMock
            //    .Setup(x => x.DeleteFollowerAsync(
            //        It.Is<int>(y => y == 48)))
            //    .Returns(Task.CompletedTask);

            var socialServiceMock = new Mock<ISocialMediaService>(MockBehavior.Strict);
            socialServiceMock
                .Setup(x => x.UserDal.GetFollowersAsync(
                    It.Is<int>(y => y == twitter.Id)
                    ))
                .ReturnsAsync(followers.ToArray());

            socialServiceMock
                .Setup(x => x.UserDal.RemoveFollower(
                    It.Is<int>(y => y == followers[0].Id),
                    It.Is<int>(y => y == twitter.Id)
                    ))
                .Returns(Task.CompletedTask);
            
            socialServiceMock
                .Setup(x => x.UserDal.DeleteUserAsync(
                    It.Is<int>(y => y == twitter.Id)
                    ))
                .Returns(Task.CompletedTask);
            

            var rejectFollowingActionMock = new Mock<IRejectFollowingAction>(MockBehavior.Strict);
            rejectFollowingActionMock
                .Setup(x => x.ProcessAsync(
                    It.Is<Follower>(y => y.Id == 48),
                    It.Is<SyncTwitterUser>(y => y.Acct == twitter.Acct)))
                .Returns(Task.CompletedTask);
            #endregion

            var action = new RemoveTwitterAccountAction(followersDalMock.Object, socialServiceMock.Object, rejectFollowingActionMock.Object);
            await action.ProcessAsync(twitter);

            #region Validations
            followersDalMock.VerifyAll();
            rejectFollowingActionMock.VerifyAll();
            #endregion
        }

        [TestMethod]
        public async Task ProcessAsync_KeepFollower()
        {
            #region Stubs
            var twitter = new SyncTwitterUser
            {
                Id = 24,
                Acct = "my-acct"
            };

            var followers = new List<Follower>
            {
                new Follower
                {
                    Id = 48,
                    Followings = new List<int>{ 24, 36 },
                }
            };
            #endregion

            #region Mocks
            var followersDalMock = new Mock<IFollowersDal>();

            var twitterUserDalMock = new Mock<ISocialMediaService>(MockBehavior.Strict);
            twitterUserDalMock
                .Setup(x => x.UserDal.GetFollowersAsync(
                    It.Is<int>(y => y == twitter.Id)
                    ))
                .ReturnsAsync(followers.ToArray());

            twitterUserDalMock
                .Setup(x => x.UserDal.RemoveFollower(
                    It.Is<int>(y => y == followers[0].Id),
                    It.Is<int>(y => y == twitter.Id)
                    ))
                .Returns(Task.CompletedTask);
            
            twitterUserDalMock
                .Setup(x => x.UserDal.DeleteUserAsync(
                    It.Is<int>(y => y == twitter.Id)
                    ))
                .Returns(Task.CompletedTask);
            
            
            var rejectFollowingActionMock = new Mock<IRejectFollowingAction>(MockBehavior.Strict);
            rejectFollowingActionMock
                .Setup(x => x.ProcessAsync(
                    It.Is<Follower>(y => y.Id == 48),
                    It.Is<SyncTwitterUser>(y => y.Acct == twitter.Acct)))
                .Returns(Task.CompletedTask);
            #endregion

            var action = new RemoveTwitterAccountAction(followersDalMock.Object, twitterUserDalMock.Object, rejectFollowingActionMock.Object);
            await action.ProcessAsync(twitter);

            #region Validations
            followersDalMock.VerifyAll();
            twitterUserDalMock.VerifyAll();
            rejectFollowingActionMock.VerifyAll();
            #endregion
        }
    }
}