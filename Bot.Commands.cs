using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Discord;
using Discord.WebSocket;

namespace ZCABot
{
    public partial class Bot
    {
        private const ulong HighlightChannelID = 480611202679177218;
        private const double MinutesBetweenHighlights = 15.0;

        private static readonly ulong[] JoinRoleIDs =
        {
            616429614620868646,
            559930385711104000,
            559930515055050752
        };

        private static readonly ulong[] AdminRoleIDs =
        {
            384822787409444886,
            133076640509984768
        };

        private static readonly string[][] HelpCommands =
        {
            new[]
            {
                "addRole [role]",
                "Adds an auto-join applied role from your username. You must omit spaces in the role name, the bot will match it correctly."
            },
            new[]
            {
                "highlight [role] [message]",
                "Provides a highlight in the main channel (ex: `.highlight teamgame Come Priv!`)"
            },
            new[]
            {
                "removeRole [role]",
                "Removes an auto-join applied role from your username. You must omit spaces in the role name, the bot will match it correctly."
            }
        };

        private static readonly string[][] HelpCommandsStaff =
        {
            new[]
            {
                "giveRole [name] [role] [amount] [min(s)/hour(s)/day(s)]",
                "Gives a role to a user for some period of time. Time must be mins/hours/days (ex: `.giveRole SomeTeam Zakken 30 mins`, or `.giveRole water muted 1 day`)"
            }
        };

        private static readonly string[][] HelpCommandsManager =
        {
            new[]
            {
                "crash",
                "Crashes the bot so I can make sure it restarts properly"
            }
        };

        private DateTime HighlightTime;

        private bool IsAdmin(SocketUser user) => AdminRoleIDs.Contains(user.Id);

        private bool CanHighlight(out TimeSpan timeUntil)
        {
            DateTime next = HighlightTime.AddMinutes(MinutesBetweenHighlights);
            timeUntil = next - DateTime.Now;

            TimeSpan delta = DateTime.Now - HighlightTime;
            return delta.TotalMinutes >= MinutesBetweenHighlights;
        }

        private void HandleAddRole(List<string> tokens, SocketMessage msg)
        {
            HandleAddOrRemoveRole(tokens, msg, true);
        }

        private void HandleRemoveRole(List<string> tokens, SocketMessage msg)
        {
            HandleAddOrRemoveRole(tokens, msg, false);
        }

        private void HandleAddOrRemoveRole(List<string> tokens, SocketMessage msg, bool add)
        {
            Log($"{msg.Author} requesting role update ({string.Join(" ", tokens)})");

            IMessageChannel channel = msg.Channel;

            if (tokens.Count < 2)
            {
                channel.SendMessageAsync("Not enough arguments. PM the bot with `.help` for usage instructions!");
                return;
            }

            string roleName = tokens[1];
            IRole? role = FindRoleFromName(roleName);
            if (role == null)
            {
                channel.SendMessageAsync($"No such role `{roleName}`. Did you spell it correctly? If the role has a space, contact a developer.");
                return;
            }

            if (!JoinRoleIDs.Contains(role.Id))
            {
                channel.SendMessageAsync("The role provided is not a role that can be updated on your account.");
                return;
            }

            IGuildUser? user = Guild.GetUserAsync(msg.Author.Id).Result;
            if (user == null)
            {
                channel.SendMessageAsync("You do not belong to the guild.");
                return;
            }

            if (add)
                user.AddRoleAsync(role).Wait();
            else
                user.RemoveRoleAsync(role).Wait();
            channel.SendMessageAsync($"Role {role.Name} updated.");
        }

        private void HandleHighlight(List<string> tokens, SocketUser sender, ITextChannel channel)
        {
            Log($"Highlight requested in channel {channel} ({string.Join(" ", tokens)})");

            if (channel.Id != HighlightChannelID)
            {
                channel.SendMessageAsync("Please use the main channel for highlights.");
                return;
            }

            if (tokens.Count < 3)
            {
                channel.SendMessageAsync("Not enough arguments. PM the bot with `.help` for usage instructions!");
                return;
            }

            string roleName = tokens[1];
            IRole? role = FindRoleFromName(roleName);
            if (role == null)
            {
                channel.SendMessageAsync($"No such role `{roleName}`. Did you spell it correctly? If the role has a space, contact a developer.");
                return;
            }

            if (!JoinRoleIDs.Contains(role.Id))
            {
                channel.SendMessageAsync("The role provided cannot be highlighted.");
                return;
            }

            if (!CanHighlight(out TimeSpan delta))
            {
                channel.SendMessageAsync($"Someone has highlighted recently. Please wait {delta.Minutes} minutes and {delta.Seconds} seconds before trying again!");
                return;
            }

            string message = string.Join(" ", tokens.Skip(2));

            role.ModifyAsync(prop => { prop.Mentionable = true; }).Wait();
            HighlightTime = DateTime.Now;
            channel.SendMessageAsync($"{role.Mention} (from {sender.Username}): {message}").Wait();
            role.ModifyAsync(prop => { prop.Mentionable = false; }).Wait();
        }

        private void HandleHelpRequest(List<string> tokens, SocketMessage msg, IDMChannel channel)
        {
            Log($"{msg.Author} requesting help ({string.Join(" ", tokens)})");

            StringBuilder builder = new StringBuilder();
            builder.Append("```Available commands:```\n");
            foreach (string[] commandList in HelpCommands)
                builder.Append($"`.{commandList[0]}`\n{commandList[1]}\n\n");

            if (IsStaffOrManager(msg.Author))
            {
                builder.Append("```Staff Commands:```\n");
                foreach (string[] commandList in HelpCommandsStaff)
                    builder.Append($"`.{commandList[0]}`\n{commandList[1]}\n\n");
            }

            if (IsManager(msg.Author))
            {
                builder.Append("```Administrator Commands:```\n");
                foreach (string[] commandList in HelpCommandsManager)
                    builder.Append($"`.{commandList[0]}`\n{commandList[1]}\n\n");
            }

            channel.SendMessageAsync(builder.ToString());
        }
    }
}
