using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System.Reflection;
using Newtonsoft.Json;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using Daddy.Database;
using System.Collections.Generic;
using Discord.Audio;
using Daddy.Modules.Audio;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using DiscordBotsList;
using DiscordBotsList.Api;
using Daddy.Ext;
using static Daddy.Modules.BaseCommands;

//dev version

namespace Daddy.Main
{
    public class Program
    {
        static void Main(string[] args) => new Program().Start(args).GetAwaiter().GetResult();

        public static CommandService _commands;
        public static DiscordSocketClient _client;
        private CancellationTokenSource _cts;
        private static AudioModule _audiom;
        private static AudioService _audios;
        private IServiceProvider _services;
        public static Random _ran = new Random();
        public static Program _instance = new Program();
        JSON jSon = new JSON();
        Ext._vMem _vMem = new Ext._vMem();
        public static bool Ready = false;
        public static readonly string token = JSON.GetToken();
        public static DiscordBotListApi DblApi = new DiscordBotListApi();
        public static AuthDiscordBotListApi DblApiBot;
        public Ext.Ext ObjectMemory { get; private set; }

        DateTime startTime = DateTime.Now;

        public async Task Start(string[] args)
        {
            await DoBotStuff();

            await Task.Delay(-1);
        }

        public void Stop()
        {
            _cts.Cancel();
        }

        public async Task DoBotStuff()
        {
            _instance = this;
            _cts = new CancellationTokenSource();
            _commands = new CommandService();
            _audios = new AudioService();
            _audiom = new AudioModule(_audios);
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info,
                MessageCacheSize = 50,
                AlwaysDownloadUsers = true
                //TotalShards = 2
            });
            _services = new ServiceCollection().AddSingleton(_client).AddSingleton(_commands).AddSingleton<AudioService>().BuildServiceProvider();

            _client.Log += Log;

            await InstallCommands();
            ConfigureEventHandlers();
            CancellationTokenSource tokenSource1 = new CancellationTokenSource();
            CancellationTokenSource tokenSource2 = new CancellationTokenSource();
            Task timerTask1 = RunPeriodically(_game, TimeSpan.FromSeconds(25), tokenSource1.Token);
            //Task timerTask2 = ScheduleAction(_transmit, new DateTime(2017, 12, 31, 23, 0, 0));
            //Task timerTask2 = RunPeriodically(_bumper, TimeSpan.FromHours(4.00138889), tokenSource2.Token);

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
        }

        private async Task InstallCommands()
        {
            _client.MessageReceived += HandleCommand;

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());
            //await _commands.AddModuleAsync<AudioModule>();
            //await _commands.AddModuleAsync<AudioService>();
        }

        private async Task HandleCommand(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            if (arg.Author.IsBot || message == null)
            {
                if (message.Channel.Id.Equals(424510923806343178)) {
                    ulong.TryParse(message.Content, out ulong userid);
                    Modules.Currency currency = new Modules.Currency();
                    currency.Add(userid, 150, out int val);
                    IDMChannel x = await (_client.GetUser(userid)).GetOrCreateDMChannelAsync();
                    await x.SendMessageAsync($"`Thanks for voting!`\n`Shekels:` {val}{Modules.Currency.shekel}");
                    return;
                }
                return;
            }
            int argPos = 0;
            if (((message.Content.ToLower().Contains("discord.gg") || (message.Content.ToLower().Contains("discord.me"))) && !IsVip(message.Author)) && Convert.ToBoolean(_vMem._vMemory[(message.Channel as SocketGuildChannel).Guild.Id][Settings.DeleteInviteMSG])) {
                await message.DeleteAsync();
                await message.Channel.SendMessageAsync($"{message.Author.Mention} Please don't send invite links!");
            }
            else if (!Convert.ToInt32(_vMem._vMemory[(message.Channel as SocketGuildChannel).Guild.Id][Settings.CharSpam]).Equals(0) && HasConsecutiveChars(message.Content, Convert.ToInt32(Ext._vMem._vMemory[(message.Channel as SocketGuildChannel).Guild.Id][Settings.CharSpam])) && !IsVip(message.Author)) {
                await message.DeleteAsync();
                await message.Channel.SendMessageAsync($"{message.Author.Mention} Don't type nonsense!");
            }
            /*
            else if (HasConsecutiveWords(message.Content, Convert.ToInt32(jSon.Settings((message.Channel as SocketGuildChannel).Guild, Modules.BaseCommands.Settings.WordSpam))) && !Modules.BaseCommands.isVip(message.Author))
            {
                await message.DeleteAsync();
                await message.Channel.SendMessageAsync($"{message.Author.Mention} Don't type nonsense!");
            }
            else if (HasConsecutiveEmojis(message.Content, Convert.ToInt32(jSon.Settings((message.Channel as SocketGuildChannel).Guild, Modules.BaseCommands.Settings.EmojiSpam))) && !Modules.BaseCommands.isVip(message.Author))
            {
                await message.DeleteAsync();
                await message.Channel.SendMessageAsync($"{message.Author.Mention} Don't type nonsense!");
            }*/
            else if (!(message.HasCharPrefix(Convert.ToChar(_vMem._vMemory[(message.Channel as SocketGuildChannel).Guild.Id][Settings.Prefix]), ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos))) {
                return;
            }
            var context = new CommandContext(_client, message);
            var result = await _commands.ExecuteAsync(context, argPos, _services);
            if (!result.IsSuccess && result.Error != CommandError.UnknownCommand) {
                await context.Channel.SendMessageAsync(result.ErrorReason);
            }
        }

        public void ConfigureEventHandlers()
        {
            _client.Ready               += Client_Ready;
            _client.JoinedGuild         += Client_JoinedGuild;
            _client.LeftGuild           += Client_LeftGuild;
            _client.UserJoined          += Client_UserJoined;
            _client.UserLeft            += Client_UserLeft;
            _client.UserBanned          += Client_UserBanned;
            _client.UserUnbanned        += Client_UserUnbanned;
            _client.GuildAvailable      += Guild_Ready;
            _client.ChannelCreated      += Channel_Created;
            _client.ChannelDestroyed    += Channel_Destroyed;
            _client.ChannelUpdated      += Channel_Updated;
            _client.GuildMemberUpdated  += User_Updated;
            _client.GuildUpdated        += Guild_Updated;
            _client.MessageDeleted      += Message_Deleted;
            _client.ReactionAdded       += _client_ReactionAdded;
            //_client.GuildAvailable      += _client_GuildAvailable;
        }

        /*private Task _client_GuildAvailable(SocketGuild arg)
        {
            _ = arg.DownloadUsersAsync();
            return Task.CompletedTask;
        }*/

        private async Task Client_Ready()
        {
            //await Start();
            _vMem.Start();
            Ext._vMem.instance._run();
            Modules.Currency.SetEmoji();
            DblApiBot = new AuthDiscordBotListApi(_client.CurrentUser.Id, JSON.Getdbo());
            Ready = true;
            await Task.CompletedTask;
        }

        private async Task Start()
        {
            _vMem.Start();
            /*_client.Guilds.ToList().ForEach(x =>
            {
                //var token = (ulong)(long)Ext._vMem._vMemory[x.Id][Modules.BaseCommands.Settings.Premium];
            });*/
            await Task.CompletedTask;
        }

        private async Task Client_UserJoined(SocketGuildUser arg)
        {
            await SendJoinMSG(arg, null, JoinSeverity.UserJoined);
        }

        private async Task Client_UserLeft(SocketGuildUser arg)
        {
            await SendJoinMSG(arg, null, JoinSeverity.UserLeft);
        }

        private async Task Client_JoinedGuild(SocketGuild arg)
        {
            await SendJoinMSG(null, arg, JoinSeverity.BotJoined);
        }

        private async Task Client_LeftGuild(SocketGuild arg)
        {
            await SendJoinMSG(null, arg, JoinSeverity.BotLeft);
        }

        private async Task Client_UserBanned(SocketUser arg, SocketGuild sg)
        {
            noSend.Add(arg.Id);
            await Task.CompletedTask;
        }

        private async Task Client_UserUnbanned(SocketUser arg, SocketGuild sg)
        {
            noSend.RemoveAll(x => x.Equals(arg.Id));
            await Task.CompletedTask;
        }

        private async Task Channel_Created(SocketChannel channel) => await LogCHN((channel as SocketGuildChannel).Guild, $"Created channel: **{(channel as IChannel).Name}** - <#{channel.Id}> - **{channel.Id}**");

        private async Task Channel_Destroyed(SocketChannel channel) {
            await LogCHN((channel as SocketGuildChannel).Guild, $"Deleted channel: **{(channel as IChannel).Name}** - **{channel.Id}**");
            JSON.DeleteJSON((channel as SocketGuildChannel).Guild, channel.Id);
        }

        private async Task Channel_Updated(SocketChannel channel, SocketChannel channel2)
        {
            if (!((channel as IChannel).Name.Equals((channel2 as IChannel).Name))) {
                await LogCHN((channel as SocketGuildChannel).Guild,
                $"Updated channel: **{(channel as IChannel).Name}** - <#{channel.Id}> - **{channel.Id}**",
                $"**Before**\nName: `{channel}`"
                + $"\n**After**\nName: `{channel2}`");
            }
        }

        private async Task User_Updated(SocketGuildUser user, SocketGuildUser user2)
        {
            //user = user ?? user2;
            if (!user.Username.Equals(user2.Username)) {
                await LogCHN(user.Guild, $"Updated user: **{user}** - **{user.Id}**",
                    $"**Before**\nName: `{user.Username}`"
                    + $"\n**After**\nName: `{user2.Username}`");
            }
            if (!user.Nickname.Equals(user2.Nickname)) {
                await LogCHN(user.Guild, $"Updated user: **{user}** - **{user.Id}**",
                    $"**Before**\nNickname: `{GetNickname(user.Nickname)}`"
                    + $"\n**After**\nNickname: `{GetNickname(user2.Nickname)}`");
            }
            if (!user.Roles.Count.Equals(user2.Roles.Count)) {
                await LogCHN(user.Guild, $"Updated user: **{user}** - **{user.Id}**",
                    $"**Before**\nRoles: `{GetRoles(user.Roles)}`"
                    + $"\n**After**\nRoles: `{GetRoles(user2.Roles)}`");
            }
        }

        private async Task Guild_Updated(SocketGuild guild, SocketGuild guild2)
        {
            if (!guild.Name.Equals(guild2.Name) || !guild.Owner.Equals(guild2.Owner) || !guild.IconUrl.Equals(guild2.IconUrl) || !guild.Channels.Count.Equals(guild2.Channels.Count) || !guild.Roles.Count.Equals(guild2.Roles.Count)) {
                await LogCHN(guild, $"Updated guild: **{guild.Name}** - **{guild.Id}**",
                    $"**Before**\nName: `{guild.Name}`\nOwner: `{guild.Owner}`\nIcontUrl: `{guild.IconUrl}`\nChannels: `{guild.Channels.Count}`\nRoles: `{guild.Roles.Count}`"
                    + $"\n**After**\nName: `{guild2.Name}`\nOwner: `{guild2.Owner}`\nIcontUrl: `{guild2.IconUrl}`\nChannels: `{guild2.Channels.Count}`\nRoles: `{guild2.Roles.Count}`");
            }
        }

        private async Task Message_Deleted(Cacheable<IMessage, ulong> arg1, ISocketMessageChannel arg2)
        {
            await arg1.GetOrDownloadAsync();
            if (arg1.Value.Attachments.Count.Equals(0) && !((ulong)(long)Ext._vMem._vMemory[(arg2 as IGuildChannel).GuildId][Settings.LogCHN]).Equals(arg1.Id)) {
                await LogCHN((arg2 as SocketGuildChannel).Guild, $"Deleted message: **{arg1.Value.Author}** - **{arg1.Value.Author.Id}**",
                    $"**Message ID:** {arg1.Id}"
                    + $"\n**Channel:** <#{arg1.Value.Channel.Id}>"
                    + $"\n**Message:** `{arg1.Value.Content}`");
            }
        }

        public async Task LogCHN(IGuild guild, string message, string message2 = null)
        {
            if (!_vMem._vMemory[guild.Id][Settings.LogCHN].Equals(0)) {
                await (_client.GetChannel((ulong)(long)Ext._vMem._vMemory[guild.Id][Settings.LogCHN]) as IMessageChannel).SendMessageAsync($"`[{DateTime.Now}]` **[** *{message}* **]**" + (string.IsNullOrEmpty(message) ? string.Empty : $"\n{message2}"));
            }
        }

        private string GetRoles(IReadOnlyCollection<SocketRole> roles)
        {
            StringBuilder sb = new StringBuilder();
            roles.Where(y => !y.IsEveryone).ToList().ForEach(x => sb.Append($"{x.Name}, "));
            return $"{(string.IsNullOrEmpty(sb.ToString()) ? "NONE" : sb.ToString().Remove(sb.Length - 2))}";
        }

        private string GetNickname(string str) => string.IsNullOrEmpty(str) ? "NONE" : str;

        private async Task<bool> Guild_Ready(SocketGuild arg) => await Task.FromResult(true);

        private async Task _client_ReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!reaction.UserId.Equals(_client.CurrentUser.Id) && (channel as IGuildChannel).GuildId.Equals(215601994507878402))
            {
                for (int x = 1; x < Enum.GetValues(typeof(Store)).Length + 1; x++) {
                    if (new Emoji($"{x}\u20e3").Name.Equals(reaction.Emote.Name))
                    {
                        Modules.Currency currency = new Modules.Currency();
                        int amount = currency.Amount(reaction.UserId);
                        IDMChannel y = await _client.GetUser(170695871510347778).GetOrCreateDMChannelAsync();
                        switch (x)
                        {
                            case 1:
                                if (amount >= (int)Enum.GetValues(typeof(Store)).GetValue(0)) {
                                    currency.Subtract(reaction.UserId, (int)Enum.GetValues(typeof(Store)).GetValue(0), out int val0);
                                    await y.SendMessageAsync($"{reaction.User.Value} - {Enum.GetValues(typeof(Store)).GetValue(0).ToString()}", false);
                                }
                                return;
                            case 2:
                                if (amount >= (int)Enum.GetValues(typeof(Store)).GetValue(1)) {
                                    currency.Subtract(reaction.UserId, (int)Enum.GetValues(typeof(Store)).GetValue(1), out int val1);
                                    await y.SendMessageAsync($"{reaction.User.Value} - {Enum.GetValues(typeof(Store)).GetValue(1).ToString()}", false);
                                }
                                return;
                            case 3:
                                if (amount >= (int)Enum.GetValues(typeof(Store)).GetValue(2)) {
                                    currency.Subtract(reaction.UserId, (int)Enum.GetValues(typeof(Store)).GetValue(2), out int val2);
                                    currency.Add_storage(reaction.UserId);
                                    await channel.SendMessageAsync($"`{reaction.User.Value.Username} bought 5k slot expansion`");
                                }
                                return;
                            case 4:
                                if (amount >= (int)Enum.GetValues(typeof(Store)).GetValue(3)) {
                                    currency.Subtract(reaction.UserId, (int)Enum.GetValues(typeof(Store)).GetValue(3), out int val3);
                                    await y.SendMessageAsync($"{reaction.User.Value} - {Enum.GetValues(typeof(Store)).GetValue(3).ToString()}", false);
                                }
                                return;
                            case 5:
                                if (amount >= (int)Enum.GetValues(typeof(Store)).GetValue(4)) {
                                    currency.Subtract(reaction.UserId, (int)Enum.GetValues(typeof(Store)).GetValue(4), out int val4);
                                    await y.SendMessageAsync($"{reaction.User.Value} - {Enum.GetValues(typeof(Store)).GetValue(4).ToString()}", false);
                                }
                                return;
                            case 6:
                                if (amount >= (int)Enum.GetValues(typeof(Store)).GetValue(5)) {
                                    currency.Subtract(reaction.UserId, (int)Enum.GetValues(typeof(Store)).GetValue(5), out int val5);
                                    await y.SendMessageAsync($"{reaction.User.Value} - {Enum.GetValues(typeof(Store)).GetValue(5).ToString()}", false);
                                }
                                return;
                            case 7:
                                if (amount >= (int)Enum.GetValues(typeof(Store)).GetValue(6)) {
                                    currency.Subtract(reaction.UserId, (int)Enum.GetValues(typeof(Store)).GetValue(6), out int val6);
                                    await y.SendMessageAsync($"{reaction.User.Value} - {Enum.GetValues(typeof(Store)).GetValue(6).ToString()}", false);
                                }
                                return;
                            case 8:
                                if (amount >= (int)Enum.GetValues(typeof(Store)).GetValue(7))
                                {
                                    currency.Subtract(reaction.UserId, (int)Enum.GetValues(typeof(Store)).GetValue(7), out int val6);
                                    await y.SendMessageAsync($"{reaction.User.Value} - {Enum.GetValues(typeof(Store)).GetValue(7).ToString()}", false);
                                }
                                return;
                        }
                    }
                }
            }
        }

        private async Task Pass(object n) => await Console.Out.WriteLineAsync($"Pass - {n}!");

        public async Task SendJoinMSG(SocketGuildUser user = null, SocketGuild guild = null, JoinSeverity j = JoinSeverity.Null)
        {
            if (!Ready) {
                return;
            }
            EmbedBuilder builder = new EmbedBuilder();
            switch (j)
            {
                case JoinSeverity.UserJoined:
                    if (user.IsBot) {
                        return;
                    }
                    if (!(Convert.ToInt64(Ext._vMem._vMemory[user.Guild.Id][Settings.JoinRole])).Equals(0))
                    {
                        try {
                            await user.AddRoleAsync(user.Guild.GetRole(Convert.ToUInt64(Ext._vMem._vMemory[user.Guild.Id][Settings.JoinRole])) as IRole);
                        }
                        catch (Exception e) {
                            await Log($"AddRoleAsync - {user.Guild.Id}", exception: e);
                        }
                    }
                    if (jSon.CheckPermChn(user.Guild, user.Guild.TextChannels.FirstOrDefault().Id, Commands.Welcome))
                    {
                        builder.Description = EditMessage(Convert.ToString(Ext._vMem._vMemory[user.Guild.Id][Settings.Welcome]), user as IGuildUser, null, user.Guild as IGuild);
                        builder.WithThumbnailUrl(user.GetAvatarUrl(ImageFormat.Auto, (ushort)128));
                        builder.WithAuthor(new EmbedAuthorBuilder() { IconUrl = user.GetAvatarUrl(ImageFormat.Auto, (ushort)128), Name = $"{user.Username} joined {user.Guild.Name}!" });
                    }
                    else
                    {
                        return;
                    }
                    break;
                case JoinSeverity.UserLeft:
                    if (!noSend.Contains(user.Id))
                    {
                        if (jSon.CheckPermChn((user.Guild as IGuild), user.Guild.TextChannels.FirstOrDefault().Id, Commands.Leave))
                        {
                            builder.Description = EditMessage(Convert.ToString(Ext._vMem._vMemory[user.Guild.Id][Settings.Leave]), user as IGuildUser, null, user.Guild as IGuild);
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                    break;
                case JoinSeverity.BotJoined:
                    await SendWelcomeMsg(guild);
                    return;
                case JoinSeverity.BotLeft:
                    JSON.DeleteJSON(guild as IGuild);
                    builder.Description = string.Empty;
                    break;
                case JoinSeverity.Null:
                    builder.Description = $"**NULL**";
                    break;
                default:
                    builder.Description = $"**?NULL**";
                    break;
            }
            builder.Color = user.Roles.MaxBy(x => x.Position).Color;
            //await (user.Guild.TextChannels.OrderBy(c => c.Id).FirstOrDefault() as IMessageChannel).SendMessageAsync(string.Empty, false, embed: builder.WithCurrentTimestamp().Build());
            if (Convert.ToUInt64(_vMem._vMemory[user.Guild.Id][Settings.WelcomeCHN]).Equals(0)) {
                Modules.BaseCommands _bc = new Modules.BaseCommands();
                await _bc.SetWelcome_main(user.Guild.TextChannels.OrderBy(c => c.Id).FirstOrDefault() as ITextChannel);
            }
            else if (jSon.CheckPermChn(user.Guild, user.Guild.DefaultChannel.Id, Commands.Welcome) && user.Guild.CurrentUser.GuildPermissions.Administrator)
            {
                try
                {
                    await user.Guild.GetTextChannel((ulong)(long)_vMem._vMemory[user.Guild.Id][Settings.WelcomeCHN]).SendMessageAsync(string.Empty, false, embed: builder.WithCurrentTimestamp().Build());
                }
                catch (Exception e)
                {
                    await Log($"Send welcome / leave message! - {user.Guild.Id} - {user.Guild.Name}", exception: e);
                }
                //await (_client.GetChannel((ulong)(long)Ext._vMem._vMemory[user.Guild.Id][Modules.BaseCommands.Settings.WelcomeCHN]) as ITextChannel).SendMessageAsync(string.Empty, false, embed: builder.WithCurrentTimestamp().Build());
            }
        }

        public static async Task Log(string message, LogSeverity severity = LogSeverity.Info, [System.Runtime.CompilerServices.CallerMemberName] string source = "", Exception exception = null)
        {
            await Console.Error.WriteLineAsync($"[{severity}][{source}] {message}");
            if (exception != null) {
                await Console.Error.WriteLineAsync(exception.ToString());
                await Console.Error.WriteLineAsync();
            }
        }

        public static bool HasConsecutiveChars(string source, int sequenceLength)
        {
            return Regex.IsMatch(source, "([a-zA-Z])\\1{" + (sequenceLength - 1) + "}");//WORKS
        }

        public static bool HasConsecutiveWords(string source, int sequenceLength)
        {
            return Regex.IsMatch(source, @"((\b(\w+)\s+){" + (sequenceLength - 1) + @"}\3\b)");//NEEDS FIX - (\b(\w+)\s+){" + (sequenceLength - 1) + @"}\1\b"
        }

        public static bool HasConsecutiveEmojis(string source, int sequenceLength)
        {
            return Regex.IsMatch(source, @"((\:.*?\:+)\s+){" + (sequenceLength - 1) + @"\1");//DOESN'T WORK
        }

        async Task RunPeriodically(Action action, TimeSpan interval, CancellationToken token)
        {
            while (true) {
                action();
                await Task.Delay(interval, token);
            }
        }

        async Task ScheduleAction(Action action, DateTime ExecutionTime)
        {
            await Task.Delay((int)ExecutionTime.Subtract(DateTime.Now).TotalMilliseconds);
            action();
        }

        public async void _game()
        {
            if (Ready) {
                int guilds = _client.Guilds.Count;
                int members = 0;
                foreach (SocketGuild guild in _client.Guilds) {
                    members += guild.MemberCount;
                }
                await Task.Delay(1750);
                string[] games = new string[] {
                    $"Uptime: {(int)(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalHours} hours",
                    $"Shard: {_client.ShardId} / 1",
                    $"in {guilds} guilds #{_client.ShardId}",
                    $"Latency: {_client.Latency}ms",
                    $"{GC.GetTotalMemory(true) / 1000000} Megabytes used",
                    $"daddybot.me | .help",
                    $"daddybot.me | .invite",
                    $"with {members} users!" };
                await _client.SetGameAsync(games[_ran.Next(games.Length)]);
            }
            else {
                await Task.Delay(3750);
                _game();
            }
        }

        public async void _transmit()
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = ":confetti_ball: Happy New Year! :confetti_ball:",
                Description = "Be the better version of yourself this new year. Fall, learn, carry on, repeat. Happy New Year!",
                Color = new Color((byte)(_ran.Next(255)), (byte)(_ran.Next(255)), (byte)(_ran.Next(255)))
            };
            _client.Guilds.ToList().ForEach(async x => 
            {
                await (_client.GetChannel(x.TextChannels.OrderBy(z => z.Id).FirstOrDefault().Id) as IMessageChannel).SendMessageAsync(string.Empty, embed: builder.WithFooter(y => y.WithText("Pat#1158")).Build());
            });
            await Task.CompletedTask;
        }

        public async Task SendWelcomeMsg(SocketGuild arg)
        {
            await Task.Delay(350);
            Modules.BaseCommands _bc = new Modules.BaseCommands();
            if (!DoesExistSettings(arg as IGuild)) {
                CreateSettings(arg as IGuild);
            }
            if (!DoesExistJson(arg as IGuild)) {
                CreateJson(arg as IGuild);
            }
            await Task.Delay(150);
            await _bc.SetWelcome_main(arg.TextChannels.OrderBy(c => c.Id).FirstOrDefault() as IMessageChannel);
            EmbedBuilder embed = new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = $"{_client.CurrentUser.Username} joined {arg.Name}!",
                    IconUrl = _client.CurrentUser.GetAvatarUrl()
                },
                Title = $"{_client.CurrentUser}",
                Description = $"Bot made by: [Pat](http://daddybot.me/)\nBot made for: [M&D](https://discord.gg/D6qd4BE)\nBot created: {_client.CurrentUser.CreatedAt.Day}/{_client.CurrentUser.CreatedAt.Month}/{_client.CurrentUser.CreatedAt.Year} [D/M/Y]\nType .help for command info",
                ThumbnailUrl = _client.CurrentUser.GetAvatarUrl(),
                Color = new Color((byte)(_ran.Next(255)), (byte)(_ran.Next(255)), (byte)(_ran.Next(255)))
            };
            await (arg.TextChannels.OrderBy(c => c.Id).FirstOrDefault() as IMessageChannel).SendMessageAsync(string.Empty, false, embed: embed.WithFooter(y => y.WithText(arg.Name)).WithCurrentTimestamp().Build());
            _vMem._run();
        }

        public enum JoinSeverity
        {
            Null = -1,
            UserJoined = 0,
            UserLeft = 1,
            BotJoined = 2,
            BotLeft = 3
        }

        private static Task Log(LogMessage arg)
        {
            var cc = Console.ForegroundColor;
            switch (arg.Severity) {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;//red
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;//yellow
                    break;
                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.White;//white
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.Blue;//dark gray
                    break;
            }
            Console.WriteLine($"{DateTime.Now,-19} [{arg.Severity,8}] {arg.Source}: {arg.Message}");
            Console.ForegroundColor = cc;
            return Task.CompletedTask;
        }
    }
}
