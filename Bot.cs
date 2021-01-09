using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace ZCABot
{
    public partial class Bot
    {
        private const char CommandCharacter = '.';
        private const string WelcomeMessage = "Welcome to the ZCA! You have been assigned any join roles, which you may remove by interacting with this bot. For help, type `.help` to me!";
        private const string DebugChannelName = "bot-logs";
        private const long GuildID = 480611202679177216;
        private const ulong ManagerRoleID = 480909038108934195;
        private const ulong StaffRoleID = 480911020827869194;
        private static readonly string[] JoinRoleNames = { "TeamGame", "Practice", "Duel", "Activation pending" };
        private static readonly Regex UnknownCommandRegex = new Regex(CommandCharacter + @"\w\w.+");

        private readonly DiscordSocketClient client;

        private IGuild Guild => client.GetGuild(GuildID);

        public Bot()
        {
            DiscordSocketConfig config = new DiscordSocketConfig { AlwaysDownloadUsers = true };
            client = new DiscordSocketClient(config);

            client.Log += LogAsync;
            client.Ready += ReadyAsync;
            client.MessageReceived += MessageReceivedAsync;
            client.UserJoined += UserJoinedAsync;
            client.UserLeft += UserLeftAsync;
            client.GuildMemberUpdated += GuildMemberUpdatedAsync;
        }

        public async Task RunAsync()
        {
            string token = await File.ReadAllTextAsync(".auth");
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
            StartEventLoopThread();
        }

        private bool IsSelf(SocketUser user) => user.Id == client.CurrentUser.Id;

        private bool IsStaffOrManager(SocketUser user) => IsManager(user) || IsStaff(user);

        private bool IsManager(SocketUser user) => HasRole(user, ManagerRoleID);

        private bool IsStaff(SocketUser user) => HasRole(user, StaffRoleID);

        private void LogToDebugChannel(string message) => LogToChannel(DebugChannelName, message);

        private void LogToChannel(string channelName, string message)
        {
            if (!Guild.Available)
                return;

            IGuildChannel? channel = FindChannelFromName(channelName).Result;
            if (channel == null)
                return;

            // This technically should fail, I don't see an inheritance relation?
            // Guild.GetChannelsAsync(channelName).SendMessageAsync(message).Wait()?
            ITextChannel? textChannel = (ITextChannel)channel;
            if (textChannel == null)
                return;

            textChannel.SendMessageAsync(message).Wait();
        }

        private void Log(string message)
        {
            Console.WriteLine(message);
            LogToDebugChannel(message);
        }

        private bool HasRole(SocketUser user, ulong roleID)
        {
            IGuildUser? guildUser = Guild.GetUserAsync(user.Id).Result;
            return guildUser != null && guildUser.RoleIds.Contains(roleID);
        }

        private Task LogAsync(LogMessage msg)
        {
            Console.WriteLine($">>> {msg.Message}");
            return Task.CompletedTask;
        }

        private Task ReadyAsync()
        {
            Log("Bot online and listening for commands!");
            StartEventLoopThread();
            return Task.CompletedTask;
        }

        private Task GuildMemberUpdatedAsync(SocketGuildUser before, SocketGuildUser after)
        {
            PrintRoleUpdates(before, after);
            return Task.CompletedTask;
        }

        private IEnumerable<IRole> GetJoinRoles()
        {
            List<IRole> roles = new List<IRole>();
            foreach (string roleName in JoinRoleNames)
            {
                IRole? role = FindRoleFromName(roleName);
                if (role != null)
                    roles.Add(role);
                else
                    Log($"ERROR: Failed to find join role {roleName}");
            }

            return roles;
        }

        private static bool IsNewAccount(ulong snowflake)
        {
            long unixTimestamp = (long)((snowflake >> 22) + 1420070400000);
            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(unixTimestamp);
            DateTime dateTime = dateTimeOffset.UtcDateTime;
            TimeSpan timeSinceJoin = (DateTime.Now - dateTime);

            return timeSinceJoin.Days < 7;
        }

        private async Task UserJoinedAsync(SocketGuildUser user)
        {
            if (IsNewAccount(user.Id))
            {
                LogToDebugChannel($"Banning newly created user: {user.Username}");
                await user.BanAsync();
            }
            else
                await GiveJoinRoles(user);
        }

        private Task UserLeftAsync(SocketGuildUser user)
        {
            LogToChannel(StaffTrackerChannel, $"{user.Username} left the server");
            return Task.CompletedTask;
        }

        private IRole? FindRoleFromName(string name)
        {
            string lowerName = name.ToLower();
            return Guild.Roles.FirstOrDefault(role => role.Name.ToLower() == lowerName);
        }

        private async Task<IGuildChannel?> FindChannelFromName(string channelName)
        {
            string lowerName = channelName.ToLower();
            IReadOnlyCollection<IGuildChannel> channels = await Guild.GetChannelsAsync();

            return channels.FirstOrDefault(channel => channel.Name.ToLower() == lowerName);
        }

        private void HandleChatMessage(SocketMessage msg, ITextChannel channel)
        {
            List<string> tokens = msg.Content.Split(' ').Where(s => s.Length != 0).ToList();
            if (tokens.Count <= 0)
                return;

            if (tokens[0].Length == 0 || tokens[0][0] != CommandCharacter)
                return;

            switch (tokens[0].Substring(1).ToUpper())
            {
            case "HIGHLIGHT":
                HandleHighlight(tokens, msg, msg.Author, channel);
                break;
            default:
                HandleUnexpectedCommand(tokens[0], channel);
                break;
            }
        }

        private void HandleUnexpectedCommand(string token, ITextChannel channel)
        {
            if (channel.Id != HighlightChannelID || !UnknownCommandRegex.IsMatch(token))
                return;

            channel.SendMessageAsync($"Unknown command '{token}'");
        }

        private void HandleDirectMessage(SocketMessage msg, IDMChannel channel)
        {
            if (!Guild.Available)
            {
                Log($"Guild unavailable, cannot deliver message from {msg.Author.Username}");
                msg.Author.SendMessageAsync("ERROR: Bot connection to Discord, it says our guild does not exist. Try again shortly!");
                return;
            }

            List<string> tokens = msg.Content.Split(' ').Where(s => s.Length != 0).ToList();
            if (tokens.Count <= 0)
                return;

            switch (tokens[0].Substring(1).ToUpper())
            {
            case "ADDROLE":
                HandleAddRole(tokens, msg);
                break;
            case "CRASH":
                Log($"{msg.Author.Username} invoking bot crash...");
                if (IsManager(msg.Author))
                    Environment.Exit(1);
                break;
            case "GIVEROLE":
                if (IsStaffOrManager(msg.Author))
                    GiveTemporaryRole(msg);
                break;
            case "HELP":
                HandleHelpRequest(tokens, msg, channel);
                break;
            case "REMOVEROLE":
                HandleRemoveRole(tokens, msg);
                break;
            }
        }

        private Task MessageReceivedAsync(SocketMessage msg)
        {
            if (IsSelf(msg.Author))
                return Task.CompletedTask;

            switch (msg.Channel)
            {
            case IDMChannel channel:
                HandleDirectMessage(msg, channel);
                break;
            case ITextChannel channel:
                HandleChatMessage(msg, channel);
                break;
            }

            return Task.CompletedTask;
        }
    }
}
