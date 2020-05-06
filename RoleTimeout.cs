using System;

namespace ZCABot
{
    public class RoleTimeout
    {
        public readonly ulong UserID;
        public readonly ulong RoleID;
        public readonly DateTime Expiration;

        public RoleTimeout(ulong userID, ulong roleID, DateTime expiration)
        {
            UserID = userID;
            RoleID = roleID;
            Expiration = expiration;
        }
    }
}
