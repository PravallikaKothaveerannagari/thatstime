using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using webapi.Models;
using System.Security.Claims;
using System.Collections.Generic;

namespace webapi.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("api/records")]
    public class RecordsController : MyBaseController
    {
        public DataContext DataContext;

        public RecordsController(DataContext ctx)
        {
            DataContext = ctx;
        }

        [HttpPost("newrecord")]
        public async Task<IActionResult> postRecordAsync([FromBody] RecordFromFrontEnd recordFromFrontEnd)
        {
            RecordResponse response = new RecordResponse();

            if (!recordFromFrontEnd.isValid())
                return Ok(response);

            try
            {
                GroupsCreatorsList? relatedGroup = null;
                UserInfo? relatedUser = null;
                long selectedObjectId = 0;
                if (!recordFromFrontEnd.yourSelf)
                {
                    if (recordFromFrontEnd.showGroupList)
                        relatedGroup = await DataContext.GroupsCreatorsLists.SingleOrDefaultAsync(obj => obj.GroupName == recordFromFrontEnd.selectedObject);
                    else
                        relatedUser = await DataContext.UserInfo.SingleOrDefaultAsync(obj => obj.UserName == recordFromFrontEnd.selectedObject);

                    if (recordFromFrontEnd.showGroupList ? relatedGroup == null : relatedUser == null)
                        return Ok(response);
                    selectedObjectId = recordFromFrontEnd.showGroupList ? relatedGroup.GroupId : relatedUser.UserId;
                }

                UserInfo? mainUser = await DataContext.UserInfo.SingleOrDefaultAsync(obj => obj.UserName == getUserName());

                if (mainUser == null)
                    return Ok(response);

                if (!recordFromFrontEnd.yourSelf && !(await canUserMakeAction(selectedObjectId, mainUser.UserId, recordFromFrontEnd.showGroupList)))
                    return Ok(response);

                Record record = new Record(recordFromFrontEnd, selectedObjectId, mainUser.UserId);

                await DataContext.Records.AddAsync(record);
                await DataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }

            response.Success = true;
            response.Message = "Record is created!";

            return Ok(response);
           
        }

        [HttpGet("certain")]
        public async Task<IActionResult> getCertainRecordAsync([FromQuery] CertainRecord certainRecord)
        {
            RecordResponse response = new RecordResponse();
            List<Record> recordsRaw = new List<Record>();
            DateTime date = new DateTime(certainRecord.Year, certainRecord.Month, certainRecord.Day);
            try
            { 
                UserInfo? mainUser = await DataContext.UserInfo.SingleOrDefaultAsync(obj => obj.UserName == getUserName()); 
                if (mainUser == null)
                    return Ok(response);
                if (certainRecord.ForYourSelf)
                {
                    recordsRaw.AddRange(await DataContext.Records
                        .Include(obj => obj.RelatedGroup)
                        .Include(obj => obj.RelatedUser)
                        .Include(obj => obj.CreatorUser)
                        .Where(obj => (obj.RelatedUserId == mainUser.UserId || obj.CreatorId == mainUser.UserId || 
                        obj.RelatedGroup.GroupMembers.Where(group => group.MemberId != mainUser.UserId).Count() != 0) && obj.DateTime.Date == date.Date).ToListAsync());
                }
                else
                {
                    GroupMemberList? relatedGroup = null;
                    UserInfo? relatedUser = null;
                    if (certainRecord.IsGroup)
                        relatedGroup = await DataContext.GroupMemberLists.SingleOrDefaultAsync(obj => obj.MemberId == mainUser.UserId && obj.RelatedGroup.GroupName == certainRecord.RelatedObject);
                    else
                        relatedUser = await DataContext.UserInfo.SingleOrDefaultAsync(obj => obj.UserName == certainRecord.RelatedObject);

                    if (certainRecord.IsGroup ? relatedGroup == null : relatedUser == null)
                        return Ok(response);

                    long relatedObjectId = certainRecord.IsGroup ? relatedGroup.GroupId : relatedUser.UserId;

                    recordsRaw = await DataContext.Records
                        .Include(obj => obj.RelatedUser)
                        .Include(obj => obj.RelatedGroup)
                        .Include(obj => obj.CreatorUser)
                        .Where(obj => (certainRecord.IsGroup ? obj.RelatedGroupId == relatedObjectId :
                            ((obj.RelatedUserId == relatedObjectId && obj.CreatorId == mainUser.UserId) || (obj.RelatedUserId == mainUser.UserId && obj.CreatorId == relatedObjectId))) && obj.DateTime.Date == date.Date)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }

            response.Success = true;
            response.Message = "Got all records";
            response.Records.AddRange(RecordFromFrontEnd.transformToFrontendRecords(recordsRaw));
            return Ok(response);
        }

        [HttpGet("friend")]
        public async Task<IActionResult> getRecordsWithFriendAsync([FromQuery]string friendName, [FromQuery] int year, [FromQuery] int month, [FromQuery] int day)
        {
            RecordResponse response = new RecordResponse();
            DateTime sevenDaysAgo = new DateTime(year, month, day).AddDays(-7);
            DateTime sevenDaysLater = new DateTime(year, month, day).AddDays(7);
            try
            {
                string mainUsername = getUserName();

                FriendsList? areFriends = await DataContext.FriendsLists.SingleOrDefaultAsync(obj => (obj.FirstUserInfo.UserName == mainUsername && obj.SecondUserInfo.UserName == friendName) ||
                    (obj.FirstUserInfo.UserName == friendName && obj.SecondUserInfo.UserName == mainUsername));
                if(areFriends == null)
                {
                    response.Message = "This user is not your friend";
                    return Ok(response);
                }

                response.Records.AddRange(RecordFromFrontEnd.transformToFrontendRecords(await DataContext.Records
                    .Include(obj => obj.RelatedUser)
                    .Include(obj => obj.CreatorUser)
                    .Where(obj => ((obj.CreatorUser.UserName == mainUsername && obj.RelatedUser.UserName == friendName) ||
                        (obj.CreatorUser.UserName == friendName && obj.RelatedUser.UserName == mainUsername)) && obj.DateTime >= sevenDaysAgo && obj.DateTime <= sevenDaysLater)
                    .ToListAsync()));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }

            response.Success = true;
            response.Message = "Got all records";
            return Ok(response);
        }

        [HttpGet("groupinfo")]
        public async Task<IActionResult> getGroupInfoAsync([FromQuery] string groupName, [FromQuery] int year, [FromQuery] int month, [FromQuery] int day)
        {
            GroupInfo groupInfo = new GroupInfo();
            DateTime sevenDaysAgo = new DateTime(year, month, day).AddDays(-7);
            DateTime sevenDaysLater = new DateTime(year, month, day).AddDays(7);
            try
            {
                GroupsCreatorsList? group = await DataContext.GroupsCreatorsLists
                    .Include(group => group.RecordsForThisGroup.Where(obj => obj.DateTime >= sevenDaysAgo && obj.DateTime <= sevenDaysLater)).ThenInclude(obj => obj.CreatorUser)
                    .Include(group => group.GroupMembers).ThenInclude(member => member.RelatedUser)
                    .Include(group => group.GroupMembers).ThenInclude(role => role.Role)
                    .Include(group => group.Creator)
                    .SingleOrDefaultAsync(obj => obj.GroupName == groupName && obj.GroupMembers.SingleOrDefault(obj => obj.RelatedUser.UserName == getUserName()) != null);

                if (group == null)
                    return Ok(groupInfo);

                List<MemberInfo> members = new List<MemberInfo>();
                foreach(GroupMemberList member in group.GroupMembers)
                {
                    members.Add(new MemberInfo()
                    {
                        Name = member.RelatedUser.UserName,
                        Degree = member.Role.RoleName
                    });
                }

                groupInfo.Creator = group.Creator.UserName;
                groupInfo.Members = members;
                groupInfo.Records = RecordFromFrontEnd.transformToFrontendRecords(group.RecordsForThisGroup.ToList());
                groupInfo.IsMember = true;
                groupInfo.isCreator = group.Creator.UserName == getUserName() ? true : false;

            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }

            return Ok(groupInfo);
        }

        [HttpGet("recent")]
        public async Task<IActionResult> getRecentRecordsAsync([FromQuery]int year, [FromQuery] int month, [FromQuery] int day)
        {
            RecordResponse response = new RecordResponse();
            List<Record> recordsRaw = new List<Record>();
            DateTime sevenDaysAgo = new DateTime(year, month, day).AddDays(-7);
            DateTime sevenDaysLater = new DateTime(year, month, day).AddDays(7);
            try
            {
                UserInfo? mainUser = await DataContext.UserInfo
                    .Include(obj => obj.GroupMembers)
                    .SingleOrDefaultAsync(obj => obj.UserName == getUserName());
                if (mainUser == null)
                    return Ok(response);

                recordsRaw.AddRange(
                    await DataContext.Records
                    .Include(obj => obj.RelatedUser)
                    .Include(obj => obj.CreatorUser)
                    .Where(obj => (obj.RelatedUserId == mainUser.UserId || obj.CreatorId == mainUser.UserId) && obj.RelatedGroupId == null && obj.DateTime >= sevenDaysAgo && obj.DateTime <= sevenDaysLater)
                    .ToListAsync()
                );

                foreach(GroupMemberList group in mainUser.GroupMembers ?? new List<GroupMemberList>())
                {
                    recordsRaw.AddRange(await DataContext.Records
                        .Include(obj => obj.RelatedGroup)
                        .Include(obj => obj.CreatorUser)
                        .Where(obj => obj.RelatedGroupId == group.GroupId && obj.DateTime >= sevenDaysAgo && obj.DateTime <= sevenDaysLater).ToListAsync() 
                    ?? new List<Record>());
                }
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }

            response.Success = true;
            response.Message = "Got all records";
            response.Records.AddRange(RecordFromFrontEnd.transformToFrontendRecords(recordsRaw));
            return Ok(response);
        }

        private async Task<bool> canUserMakeAction(long relatedObjectId, long mainUserId, bool isGroup)
        {
            if (isGroup)
            {
                GroupMemberList? isMember = await DataContext.GroupMemberLists.SingleOrDefaultAsync(obj => obj.MemberId == mainUserId && obj.GroupId == relatedObjectId);
                if (isMember == null)
                    return false;
            }
            else
            {
                long firstUserId = Math.Max(mainUserId, relatedObjectId);
                long secondUserId = Math.Min(mainUserId, relatedObjectId);

                FriendsList? areFriends = await DataContext.FriendsLists.SingleOrDefaultAsync(obj => obj.FirstUserId == firstUserId && obj.SecondUserId == secondUserId);
                if (areFriends == null)
                    return false;
            }

            return true;
        }

        private string getUserName()
        {
            return HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name).Value;
        }
    }

    public class RecordResponse
    {
        public bool Success { get; set; } = false;

        public string Message { get; set; } = "Request has failed";

        public List<RecordFromFrontEnd> Records { get; set; } = new List<RecordFromFrontEnd>();
    }

    public class RecordFromFrontEnd
    {
        public int selectedYear { get; set; }
        public int selectedMonth { get; set; }
        public int selectedDay { get; set; }
        public bool showGroupList { get; set; }
        public bool yourSelf { get; set; }
        public string Creator { get; set; }
        public string selectedObject { get; set; }
        public int importance { get; set; }
        public int hour { get; set; }
        public int minute { get; set; }
        public string recordName { get; set; }
        public string recordContent { get; set; }
        
        public static List<RecordFromFrontEnd> transformToFrontendRecords(List<Record> recordsRaw)
        {
            List<RecordFromFrontEnd> recordFromFrontEnds = new List<RecordFromFrontEnd>();

            foreach(Record record in recordsRaw)
            {
                RecordFromFrontEnd tempRec = new RecordFromFrontEnd()
                {
                    selectedYear = record.DateTime.Year,
                    selectedMonth = record.DateTime.Month,
                    selectedDay = record.DateTime.Day,
                    showGroupList = record.IsRecordForGroup,
                    yourSelf = record.IsRecordForYourSelf,
                    selectedObject = record.IsRecordForGroup ? record.RelatedGroup.GroupName : record.RelatedUser.UserName,
                    Creator = record.CreatorUser.UserName,
                    importance = record.Importance,
                    hour = record.DateTime.Hour,
                    minute = record.DateTime.Minute,
                    recordName = record.RecordName,
                    recordContent = record.RecordContent
                };
                recordFromFrontEnds.Add(tempRec);
            }

            return recordFromFrontEnds;
        }

        public bool isValid()
        {
            if(!isDateValid() ||
                selectedObject.IsNullOrEmpty() ||
                !(0 <= hour && hour <= 23) ||
                !(0 <= minute && minute <= 59) ||
                !(recordName.Length >= 1 && recordName.Length <= 50) ||
                !(recordContent.Length >= 1 && recordContent.Length <= 500) ||
                !(importance >= 0 && importance <= 2)
                )
                return false;
           return true;
        }

        private bool isDateValid()
        {
            string format = "yyyy-MM-dd HH:mm";
            string inputDate = $"{selectedYear:D4}-{selectedMonth:D2}-{selectedDay:D2} {hour:D2}:{minute:D2}";

            // Try parsing the input string into a DateTime object
            if (DateTime.TryParseExact(inputDate, format, null, System.Globalization.DateTimeStyles.None, out _))
                return true;
            else
                return false;
        }
    }

    public class CertainRecord
    {
        public string RelatedObject { get; set; } = string.Empty;

        public bool ForYourSelf { get; set; }

        public bool IsGroup { get; set; }

        public int Year { get; set; }

        public int Month { get; set; }

        public int Day { get; set; }
    }

    public class GroupInfo
    {
        public bool IsMember { get; set; } = false;

        public bool isCreator { get; set; } = false;

        public string Creator { get; set; } = string.Empty;

        public List<MemberInfo> Members { get; set; } = new List<MemberInfo>();

        public List<RecordFromFrontEnd> Records { get; set; } = new List<RecordFromFrontEnd>();
    }

    public class MemberInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Degree { get; set; } = string.Empty;
    }
}
