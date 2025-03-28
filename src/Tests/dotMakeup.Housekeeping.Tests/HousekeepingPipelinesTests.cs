using System;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Domain.Repository;
using BirdsiteLive.Moderation.Processors;
using dotMakeup.ipfs;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace BirdsiteLive.Moderation.Tests
{
    [TestClass]
    public class HousekeepingPipelinesTests
    {
        [TestMethod]
        public async Task ApplyModerationSettingsAsync_None()
        {
            #region Mocks
            var moderationRepositoryMock = new Mock<IModerationRepository>(MockBehavior.Strict);
            moderationRepositoryMock
                .Setup(x => x.GetModerationType(ModerationEntityTypeEnum.Follower))
                .Returns(ModerationTypeEnum.None);
            moderationRepositoryMock
                .Setup(x => x.GetModerationType(ModerationEntityTypeEnum.TwitterAccount))
                .Returns(ModerationTypeEnum.None);
            
            var followerModerationProcessorMock = new Mock<IFollowerModerationProcessor>(MockBehavior.Strict);
            var twitterAccountModerationProcessorMock = new Mock<ITwitterAccountModerationProcessor>(MockBehavior.Strict);
            var loggerMock = new Mock<ILogger<Housekeeping>>();
            var ifpsMock = new Mock<IIpfsService>();
            var socialMediaServiceMock = new Mock<ISocialMediaService>();
            var settings = new InstanceSettings();
            #endregion

            var pipeline = new Housekeeping(moderationRepositoryMock.Object, followerModerationProcessorMock.Object, twitterAccountModerationProcessorMock.Object, ifpsMock.Object, socialMediaServiceMock.Object, settings, loggerMock.Object);
            await pipeline.ApplyModerationSettingsAsync();

            #region Validations
            moderationRepositoryMock.VerifyAll();
            followerModerationProcessorMock.VerifyAll();
            twitterAccountModerationProcessorMock.VerifyAll();
            loggerMock.VerifyAll();
            #endregion
        }

        [TestMethod]
        public async Task ApplyModerationSettingsAsync_Process()
        {
            #region Mocks
            var moderationRepositoryMock = new Mock<IModerationRepository>(MockBehavior.Strict);
            moderationRepositoryMock
                .Setup(x => x.GetModerationType(ModerationEntityTypeEnum.Follower))
                .Returns(ModerationTypeEnum.WhiteListing);
            moderationRepositoryMock
                .Setup(x => x.GetModerationType(ModerationEntityTypeEnum.TwitterAccount))
                .Returns(ModerationTypeEnum.BlackListing);

            var followerModerationProcessorMock = new Mock<IFollowerModerationProcessor>(MockBehavior.Strict);
            followerModerationProcessorMock
                .Setup(x => x.ProcessAsync(
                    It.Is<ModerationTypeEnum>(y => y == ModerationTypeEnum.WhiteListing)))
                .Returns(Task.CompletedTask);

            var twitterAccountModerationProcessorMock = new Mock<ITwitterAccountModerationProcessor>(MockBehavior.Strict);
            twitterAccountModerationProcessorMock
                .Setup(x => x.ProcessAsync(
                    It.Is<ModerationTypeEnum>(y => y == ModerationTypeEnum.BlackListing)))
                .Returns(Task.CompletedTask);

            var loggerMock = new Mock<ILogger<Housekeeping>>();
            var ifpsMock = new Mock<IIpfsService>();
            var socialMediaServiceMock = new Mock<ISocialMediaService>();
            var settings = new InstanceSettings();
            #endregion

            var pipeline = new Housekeeping(moderationRepositoryMock.Object, followerModerationProcessorMock.Object, twitterAccountModerationProcessorMock.Object, ifpsMock.Object, socialMediaServiceMock.Object, settings, loggerMock.Object);
            await pipeline.ApplyModerationSettingsAsync();

            #region Validations
            moderationRepositoryMock.VerifyAll();
            followerModerationProcessorMock.VerifyAll();
            twitterAccountModerationProcessorMock.VerifyAll();
            loggerMock.VerifyAll();
            #endregion
        }

        [TestMethod]
        public async Task ApplyModerationSettingsAsync_Exception()
        {
            #region Mocks
            var moderationRepositoryMock = new Mock<IModerationRepository>(MockBehavior.Strict);
            moderationRepositoryMock
                .Setup(x => x.GetModerationType(ModerationEntityTypeEnum.Follower))
                .Throws(new Exception());

            var followerModerationProcessorMock = new Mock<IFollowerModerationProcessor>(MockBehavior.Strict);
            var twitterAccountModerationProcessorMock = new Mock<ITwitterAccountModerationProcessor>(MockBehavior.Strict);
            
            var loggerMock = new Mock<ILogger<Housekeeping>>();
            var ifpsMock = new Mock<IIpfsService>();
            var socialMediaServiceMock = new Mock<ISocialMediaService>();
            var settings = new InstanceSettings();
            #endregion

            var pipeline = new Housekeeping(moderationRepositoryMock.Object, followerModerationProcessorMock.Object, twitterAccountModerationProcessorMock.Object, ifpsMock.Object, socialMediaServiceMock.Object, settings, loggerMock.Object);
            await pipeline.ApplyModerationSettingsAsync();

            #region Validations
            moderationRepositoryMock.VerifyAll();
            followerModerationProcessorMock.VerifyAll();
            twitterAccountModerationProcessorMock.VerifyAll();
            loggerMock.VerifyAll();
            #endregion
        }
    }
}
