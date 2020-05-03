using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace ZCABot
{
    public partial class Bot
    {
        private const string StaffTrackerChannel = "staff-role-tracker";

        private async Task RunDelayedTask(int milliseconds, Action func)
        {
            await Task.Delay(milliseconds);
            func();
        }

        private IGuildUser? FindUserByDisplayNameOrUsername(string name)
        {
            string lowerName = name.ToLower();
            IReadOnlyCollection<IGuildUser> users = Guild.GetUsersAsync().Result;

            foreach (IGuildUser user in users)
                if (user.Nickname != null && user.Nickname.ToLower() == lowerName)
                    return user;

            foreach (IGuildUser user in users)
                if (user.Username.ToLower() == lowerName)
                    return user;

            return null;
        }

        private void PrintRoleUpdates(SocketGuildUser before, SocketGuildUser after)
        {
            if (before.Roles.Count == after.Roles.Count)
                return;

            foreach (IRole role in before.Roles)
            {
                // If it did not get added, or it's a role we automatically
                // add, then ignore it.
                if (after.Roles.Contains(role) || JoinRoleIDs.Contains(role.Id))
                    continue;

                string message = $"Username {after.Username} lost role {role.Name}";
                Log(message);
                LogToChannel(StaffTrackerChannel, message);
            }

            foreach (IRole role in after.Roles)
            {
                // If it did not get removed, or it's a role we automatically
                // add, then ignore it.
                if (before.Roles.Contains(role) || JoinRoleIDs.Contains(role.Id))
                    continue;

                string message = $"Username {after.Username} gained role {role.Name}";
                Log(message);
                LogToChannel(StaffTrackerChannel, message);
            }
        }

        private async Task GiveJoinRoles(SocketGuildUser user)
        {
            foreach (IRole role in JoinRoles)
            {
                Log($"Adding role {role} to {user}");
                await user.AddRoleAsync(role);
            }

            Log($"Sending message to new user {user}");
            await user.SendMessageAsync(WelcomeMessage);
        }

        private void GiveTemporaryRole(List<string> tokens, SocketMessage msg)
        {
            Log($"{msg.Author} giving temporary role ({string.Join(" ", tokens)})");

            if (tokens.Count < 5)
            {
                msg.Author.SendMessageAsync("Invalid number of arguments. Send `.help` to the bot to see how to use this.");
                return;
            }

            string name = tokens[1];
            IGuildUser? user = FindUserByDisplayNameOrUsername(name);
            if (user == null)
            {
                msg.Author.SendMessageAsync($"Cannot find user by the display name or username of {name}. Did you misspell it?");
                return;
            }

            string roleName = tokens[2].ToLower();
            if (roleName != "shush")
            {
                msg.Author.SendMessageAsync("We only allow the `Shush` role to be applied for now.");
                return;
            }

            IRole? role = FindRoleFromName(roleName);
            if (role == null)
            {
                msg.Author.SendMessageAsync($"Cannot find role {roleName}, did you type it correctly? If it has a space, contact a developer.");
                return;
            }

            if (!int.TryParse(tokens[3], out int timeAmount) || timeAmount <= 0)
            {
                msg.Author.SendMessageAsync("Your duration has to be a positive number. Send `.help` to the bot to see how to use this.");
                return;
            }

            int milliseconds;
            switch (tokens[4].ToUpper())
            {
            case "MIN":
            case "MINS":
            case "MINUTE":
            case "MINUTES":
                milliseconds = 1000 * 60 * timeAmount;
                break;
            case "HR":
            case "HRS":
            case "HOUR":
            case "HOURS":
                milliseconds = 1000 * 60 * 60 * timeAmount;
                break;
            case "DAY":
            case "DAYS":
                milliseconds = 1000 * 60 * 60 * 24 * timeAmount;
                break;
            default:
                msg.Author.SendMessageAsync("Your time is an unknown type (should be min(s)/hour(s)/day(s)). Send `.help` to the bot to see how to use this.");
                return;
            }

            Log($"Applying temporary role {role.Name} to {user.Username}");
            user.AddRoleAsync(role).Wait();

            // We are ignoring the task so it runs in the background.
            RunDelayedTask(milliseconds, () =>
            {
                Log($"Removing temporary role {role.Name} from {user.Username}");
                user.RemoveRoleAsync(role);
            });
        }
    }
}
