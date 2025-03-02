using System.Linq;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;

namespace BirdsiteLive.Moderation.Actions
{
    public interface IRemoveTwitterAccountAction
    {
        Task ProcessAsync(SyncTwitterUser twitterUser);
    }

    public class RemoveTwitterAccountAction : IRemoveTwitterAccountAction
    {
        private readonly IFollowersDal _followersDal;
        private readonly ISocialMediaService _socialMediaService;
        private readonly IRejectFollowingAction _rejectFollowingAction;

        #region Ctor
        public RemoveTwitterAccountAction(IFollowersDal followersDal, ISocialMediaService socialMediaService, IRejectFollowingAction rejectFollowingAction)
        {
            _followersDal = followersDal;
            _socialMediaService = socialMediaService;
            _rejectFollowingAction = rejectFollowingAction;
        }
        #endregion

        public async Task ProcessAsync(SyncTwitterUser twitterUser)
        {
            // Check Followers 
            var twitterUserId = twitterUser.Id;
            var followers = await _socialMediaService.UserDal.GetFollowersAsync(twitterUserId);
            
            // Remove all Followers
            foreach (var follower in followers) 
            {
                // Perform undo following to user instance
                await _rejectFollowingAction.ProcessAsync(follower, twitterUser);

                // Remove following from DB
                //if (follower.Followings.Any())
                await _socialMediaService.UserDal.RemoveFollower(follower.Id, twitterUserId);
                //else
                //    await _followersDal.DeleteFollowerAsync(follower.Id);
            }

            // Remove twitter user
            await _socialMediaService.UserDal.DeleteUserAsync(twitterUserId);
        }
    }
}