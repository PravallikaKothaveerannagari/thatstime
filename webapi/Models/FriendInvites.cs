using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace webapi.Models
{
    public class FriendInvites
    {
        [Key]
        public long Id { get; set; }

        //Foreign key to UserInfo userId
        public long SenderUserId { get; set; }

        //Navigation property
        public UserInfo SenderUserInfo { get; set; }

        public long TargetUserId { get; set; }

        public UserInfo TargetUserInfo { get; set; }
    }
}
