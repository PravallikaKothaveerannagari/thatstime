using System.ComponentModel.DataAnnotations;

namespace webapi.Models
{
    public class GroupInvites
    {
        [Key]
        public long Id { get; set; }

        //Foreign key property to GroupsCreatorsList
        public long GroupId { get; set; }

        //Navigation property for GroupCreatorList
        public GroupsCreatorsList GroupEntity { get; set; }

        //Foreign key property to UserInfo
        public long TargetUserId { get; set; }
    
        //Navigation property to UserInfo
        public UserInfo UserEntity { get; set; }
    }
}
