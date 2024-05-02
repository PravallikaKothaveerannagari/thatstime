using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webapi.Models;

namespace webapi.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("api/friends")]
    public class FriendManagementController : MyBaseController
    {
        DataContext DataContext;

        public FriendManagementController(DataContext ctx)
        {
            DataContext = ctx;
        }

        [HttpGet("getusers")]
        public async Task<IActionResult> getUsersAsync([FromQuery] int page)
        {
            FriendResponse response = new FriendResponse();
            const int pageSize = 5;
            try
            {
                string mainUsername = getUserName();

                response.FriendList.AddRange(await DataContext.UserInfo
                    .Where(obj => (obj.UserName != mainUsername) &&
                        obj.FirstFromFriendList.Where(friend => friend.FirstUserInfo.UserName != mainUsername || friend.SecondUserInfo.UserName != mainUsername).Count() == 0 &&
                        obj.SecondFromFriendList.Where(friend => friend.FirstUserInfo.UserName != mainUsername || friend.SecondUserInfo.UserName != mainUsername).Count() == 0)
                    .Skip(page * pageSize)
                    .Take(pageSize)
                    .Select(obj => obj.UserName)
                    .ToListAsync());
            }
            catch(Exception ex)
            {
                return HandleException(ex);
            }

            response.Success = true;
            response.Message = "Request has succeeded";
            return Ok(response);
        }

        [HttpGet("getcertainuser")]
        public async Task<IActionResult> getCertainUserAsync([FromQuery] string username)
        {
            FriendResponse response = new FriendResponse();
            try
            {
                string mainUsername = getUserName();
                UserInfo? certainUser = await DataContext.UserInfo
                    .SingleOrDefaultAsync(obj => (obj.UserName != mainUsername) && (obj.UserName == username) &&
                        obj.FirstFromFriendList.Where(friend => friend.FirstUserInfo.UserName != mainUsername || friend.SecondUserInfo.UserName != mainUsername).Count() == 0 &&
                        obj.SecondFromFriendList.Where(friend => friend.FirstUserInfo.UserName != mainUsername || friend.SecondUserInfo.UserName != mainUsername).Count() == 0);

                if(certainUser == null)
                {
                    response.Message = "Such user doesn't exist";
                    return Ok(response);
                }

                response.FriendList.Add(certainUser.UserName);
            }
            catch(Exception ex)
            {
                return HandleException(ex);
            }

            response.Success = true;
            response.Message = "Got certain user";
            return Ok(response);
        }

        [HttpGet("getfriends")]
        public async Task<IActionResult> getFriendListAsync()
        {
            FriendResponse response = new FriendResponse();

            try
            {
                List<string> friendList = await DataContext.FriendsLists
                    .Where(obj => obj.FirstUserInfo.UserName == getUserName() || obj.SecondUserInfo.UserName == getUserName())
                    .Select(obj => obj.FirstUserInfo.UserName == getUserName() ? obj.SecondUserInfo.UserName : obj.FirstUserInfo.UserName).ToListAsync();

                response.FriendList = friendList;
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }

            response.Success = true;
            response.Message = "Request has succeeded";

            return Ok(response);
        }

        [HttpGet("sendinvite")]
        public async Task<IActionResult> SendFrienInviteToUserAsync([FromQuery]string FriendName)
        {
            FriendResponse response = new FriendResponse();

            try
            {
                if(await areFriends(FriendName))
                    return Ok(response);

                FriendInvites? friendInvite = await DataContext.FriendInvites
                    .SingleOrDefaultAsync(obj => obj.SenderUserInfo.UserName == getUserName() && obj.TargetUserInfo.UserName == FriendName);

                if (friendInvite != null)
                {
                    response.Success = true;
                    response.Message = "Such invite already exist";
                    return Ok(response);
                }

                UserInfo? mainUser = await DataContext.UserInfo.SingleOrDefaultAsync(obj => obj.UserName == getUserName());
                UserInfo? friendUser = await DataContext.UserInfo.SingleOrDefaultAsync(obj => obj.UserName == FriendName);

                FriendInvites invite = new FriendInvites()
                {
                    SenderUserId = mainUser.UserId,
                    TargetUserId = friendUser.UserId
                };

                await DataContext.FriendInvites.AddAsync(invite);
                await DataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }

            response.Success = true;
            response.Message = "Request has succeeded";

            return Ok(response);
        }

        [HttpGet("deletefriend")]
        public async Task<IActionResult> DeleteFriendAsync([FromQuery] string FriendName)
        {
            FriendResponse response = new FriendResponse();
            try
            {
                FriendsList? friendsList = await DataContext.FriendsLists.SingleOrDefaultAsync(obj => (obj.FirstUserInfo.UserName == getUserName() && obj.SecondUserInfo.UserName == FriendName) ||
                    obj.FirstUserInfo.UserName == FriendName && obj.SecondUserInfo.UserName == getUserName());
                if (friendsList == null)
                    return Ok(response);

                DataContext.FriendsLists.Remove(friendsList);
                await DataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }

            response.Success = true;
            response.Message = "Request has succeeded";

            return Ok(response);
        }

        [HttpGet("getinvites")]
        public async Task<IActionResult> getInvitesAsync()
        {
            FriendResponse response = new FriendResponse();

            try
            {
                var invites = DataContext.FriendInvites.Where(obj => obj.TargetUserInfo.UserName == getUserName()).Select(obj => obj.SenderUserInfo.UserName);

                if (invites != null)
                    response.FriendList = invites.ToList();
                else
                    response.FriendList = new List<string>();
            }
            catch(Exception ex)
            {
                return HandleException(ex);
            }

            response.Success = true;
            response.Message = "Request has succeeded";
            return Ok(response);
        }

        [HttpGet("acceptinvite")]
        public async Task<IActionResult> AcceptFriendInvite([FromQuery] string FriendName)
        {
            FriendResponse response = new FriendResponse();
            List<FriendInvites> invites = new List<FriendInvites>();
            try
            {
                var toMainUserInvite = await DataContext.FriendInvites
                    .SingleOrDefaultAsync(obj => obj.TargetUserInfo.UserName == getUserName() && obj.SenderUserInfo.UserName == FriendName);

                if (toMainUserInvite == null)
                    return Ok(response);

                var fromMainUserInvite = await DataContext.FriendInvites
                    .SingleOrDefaultAsync(obj => obj.SenderUserInfo.UserName == getUserName() && obj.TargetUserInfo.UserName == FriendName);

                if (fromMainUserInvite != null)
                    invites.Add(fromMainUserInvite);

                invites.Add(toMainUserInvite);

                long firstUserId = Math.Max(toMainUserInvite.SenderUserId, toMainUserInvite.TargetUserId);
                long secondUserId = Math.Min(toMainUserInvite.SenderUserId, toMainUserInvite.TargetUserId);

                FriendsList friendsList = new FriendsList()
                {
                    FirstUserId = firstUserId,
                    SecondUserId = secondUserId
                };

                DataContext.FriendInvites.RemoveRange(invites);
                await DataContext.FriendsLists.AddAsync(friendsList);
                await DataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }

            response.Success = true;
            response.Message = "Request has succeeded";

            return Ok(response);
        }

        [HttpGet("declineinvite")]
        public async Task<IActionResult> DeclineFriendInvite([FromQuery] string FriendName)
        {
            FriendResponse response = new FriendResponse();
            try
            {
                FriendInvites? friendInvite = await DataContext.FriendInvites.SingleOrDefaultAsync(obj => obj.TargetUserInfo.UserName == getUserName() && obj.SenderUserInfo.UserName == FriendName);
                if (friendInvite == null)
                    return Ok(response);

                DataContext.FriendInvites.Remove(friendInvite);
                await DataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }

            response.Success = true;
            response.Message = "Request has succeeded";

            return Ok(response);
        }

        private async Task<bool> areFriends(string friendName)
        {
            try
            {
                FriendsList? areFriends = await DataContext.FriendsLists.SingleOrDefaultAsync(obj => (obj.FirstUserInfo.UserName == getUserName() && obj.SecondUserInfo.UserName == friendName) ||
                    (obj.FirstUserInfo.UserName == friendName && obj.SecondUserInfo.UserName == getUserName()));
                if (areFriends == null)
                    return false;

                return true;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }
    }


    public class FriendResponse
    {
        public bool Success { get; set; } = true;

        public string Message { get; set; } = "Request has failed";

        public List<string> FriendList { get; set; } = new List<string>();
    }
}
