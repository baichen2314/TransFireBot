using Kook;
using Kook.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Kook
{
    public class KookBot<T> where T : PKM, new()
    {
        private static PokeTradeHub<T> Hub = default!;
        internal static TradeQueueInfo<T> Info => Hub.Queues.Info;

        public static KookSocketClient Client = default!;
        private static KookSettings Settings = default!;

        private static SocketTextChannel? _channel;
        private static ulong _botUserId;
        internal static ulong BotUserId => _botUserId;

        public KookBot(KookSettings settings, PokeTradeHub<T> hub)
        {
            Hub = hub;
            Settings = settings;

            var config = new KookSocketConfig
            {
                LogLevel = LogSeverity.Info,
                MessageCacheSize = 100,
                AlwaysDownloadUsers = true,
            };

            Client = new KookSocketClient(config);
            Client.Log += LogAsync;
            Client.Ready += OnReady;
            Client.MessageReceived += OnMessageReceived;

            _ = Task.Run(async () =>
            {
                await Client.LoginAsync(TokenType.Bot, settings.Token);
                await Client.StartAsync();
            });
        }

        private static Task LogAsync(LogMessage msg)
        {
            Console.WriteLine($"[Kook] {msg.Message}");
            return Task.CompletedTask;
        }

        private static string VersionName() => typeof(T) switch
        {
            Type t when t == typeof(PK8) => "剑盾",
            Type t when t == typeof(PB8) => "晶灿钻石明亮珍珠",
            Type t when t == typeof(PA8) => "阿尔宙斯",
            Type t when t == typeof(PK9) => "朱紫",
            Type t when t == typeof(PA9) => "宝可梦传说Z-A",
            _ => "未知版本",
        };

        private async Task OnReady()
        {
            _botUserId = Client.CurrentUser.Id;
            if (!ulong.TryParse(Settings.ChannelId, out var channelId) || channelId == 0)
                return;

            var guild = Client.Guilds.FirstOrDefault(g => g.TextChannels.Any(c => c.Id == channelId));
            if (guild is null)
                return;
            _channel = guild.GetTextChannel(channelId);
            if (_channel is null)
                return;

            // 分发(广播)转发: 开蛋/打团等系统消息
            EchoUtil.Forwarders.Add(msg =>
            {
                if (msg.StartsWith("https"))
                    _ = SendChannelMessagePicture(msg, channelId);
                if (msg.Contains("团") || msg.Contains("打"))
                    _ = SendChannelMessage(msg, channelId);
            });

            await SendChannelMessageAll("欢迎使用传火机器人！", channelId);
            var fileMsg = Hub.Config.Legality.AllowUseFile ? "本频道可以上传文件" : "本频道不允许上传文件";
            await SendChannelMessage($"当前版本为{VersionName()},{fileMsg}", channelId);
        }

        private Task OnMessageReceived(SocketMessage msg, SocketGuildUser user, SocketTextChannel channel)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await KookMessageHandler<T>.HandleMessage(msg, user, channel);
                }
                catch (Exception ex)
                {
                    LogUtil.LogSafe(ex, LogIdentity);
                }
            });
            return Task.CompletedTask;
        }

        private const string LogIdentity = "KookBot";

        // ==================== 频道消息 ====================

        internal static async Task<SocketTextChannel?> GetChannelAsync(ulong channelId)
        {
            if (_channel is not null && _channel.Id == channelId)
                return _channel;
            var guild = Client.Guilds.FirstOrDefault(g => g.TextChannels.Any(c => c.Id == channelId));
            var channel = guild?.GetTextChannel(channelId);
            if (channel is not null)
                _channel = channel;
            return channel;
        }

        public static async Task SendChannelMessage(string message, ulong channelId)
        {
            if (string.IsNullOrEmpty(message)) return;
            var channel = await GetChannelAsync(channelId);
            if (channel is null) return;
            await channel.SendTextAsync(message);
        }

        public static async Task SendChannelAtMessage(ulong atUserId, string message, ulong channelId)
        {
            if (string.IsNullOrEmpty(message)) return;
            var channel = await GetChannelAsync(channelId);
            if (channel is null) return;
            await channel.SendTextAsync($"(met){atUserId}(met) {message}");
        }

        public static async Task SendChannelMessageAll(string message, ulong channelId)
        {
            if (string.IsNullOrEmpty(message)) return;
            var channel = await GetChannelAsync(channelId);
            if (channel is null) return;
            await channel.SendTextAsync($"(met)all(met) {message}");
        }

        public static async Task SendChannelMessagePicture(string url, ulong channelId)
        {
            if (!IsValidImageUrl(url)) return;
            var channel = await GetChannelAsync(channelId);
            if (channel is null) return;
            var card = new CardBuilder { Theme = CardTheme.None, Size = CardSize.Large };
            card.AddModule(new ImageGroupModuleBuilder().AddElement(new ImageElementBuilder { Source = url! }));
            await channel.SendCardAsync(card.Build());
        }

        // ==================== 私信 ====================

        internal static SocketUser? GetUser(ulong userId)
        {
            foreach (var guild in Client.Guilds)
            {
                var u = guild.GetUser(userId);
                if (u is not null)
                    return u;
            }
            return null;
        }

        public static async Task SendPersonalMessage(ulong userId, string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            var user = GetUser(userId);
            if (user is null) return;
            var dm = await user.CreateDMChannelAsync();
            await dm.SendTextAsync(message);
        }

        public static async Task SendPersonalMessagePicture(string url, ulong userId)
        {
            if (!IsValidImageUrl(url)) return;
            var user = GetUser(userId);
            if (user is null) return;
            var dm = await user.CreateDMChannelAsync();
            var card = new CardBuilder { Theme = CardTheme.None, Size = CardSize.Large };
            card.AddModule(new ImageGroupModuleBuilder().AddElement(new ImageElementBuilder { Source = url! }));
            await dm.SendCardAsync(card.Build());
        }

        public static async Task SendPersonalFile(ulong userId, byte[] data, string filename)
        {
            var user = GetUser(userId);
            if (user is null) return;
            var dm = await user.CreateDMChannelAsync();
            using var ms = new MemoryStream(data);
            await dm.SendFileAsync(ms, filename, AttachmentType.File);
        }

        // ==================== 卡片消息 ====================

        // imdodo 图床已随 Dodo 停服失效, 这些地址视为不可用, 渲染时跳过对应图片
        internal static bool IsValidImageUrl(string? url) =>
            !string.IsNullOrWhiteSpace(url) && !url.Contains("img.imdodo.com") &&
            (url.StartsWith("https://") || url.StartsWith("http://"));

        public static async Task SendChannelCardMessage(string message, ulong channelId, string pokeurl, string itemurl, string ballurl, string teraurl, string teraoriginalurl, string shinyurl, string movetypeurl1, string movetypeurl2, string movetypeurl3, string movetypeurl4)
        {
            var channel = await GetChannelAsync(channelId);
            if (channel is null) return;
            var card = BuildTradeCard(message, pokeurl, itemurl, ballurl, teraurl, teraoriginalurl, shinyurl, movetypeurl1, movetypeurl2, movetypeurl3, movetypeurl4);
            await channel.SendCardAsync(card.Build());
        }

        public static async Task SendChannelEggCardMessage(string title, string message, ulong channelId, string pokeurl, string ballurl, string shinyurl, string shinyinfo)
        {
            var channel = await GetChannelAsync(channelId);
            if (channel is null) return;
            var card = BuildEggCard(title, message, pokeurl, ballurl, shinyurl, shinyinfo);
            await channel.SendCardAsync(card.Build());
        }

        public static async Task SendChannelCardBatchMessage(string message, ulong channelId, string pokeurl, string itemurl, string ballurl, string shinyurl)
        {
            var channel = await GetChannelAsync(channelId);
            if (channel is null) return;
            var card = BuildBatchCard(message, pokeurl, itemurl, ballurl, shinyurl);
            await channel.SendCardAsync(card.Build());
        }

        private static KMarkdownElementBuilder KMarkdown(string content) => new() { Content = content };
        private static ImageElementBuilder Image(string url) => new() { Source = url };
        private static PlainTextElementBuilder PlainText(string content) => new() { Content = content };

        /// <summary>
        /// 交易卡片: 解析 PokemonTradeHelper.CardInfo 生成的文本(按行)
        /// 0昵称 1性别 2性格 3特性 4等级 5大小 6Home追踪 7个体: 8个体值 9努力: 10努力值 11技能 12-15技能名 16来源版本
        /// </summary>
        private static CardBuilder BuildTradeCard(string message, params string[] urls)
        {
            var card = new CardBuilder { Theme = CardTheme.None, Size = CardSize.Large };
            if (string.IsNullOrEmpty(message)) message = "None";
            var lines = message.Split('\n');

            string pokeurl = urls.Length > 0 ? urls[0] : "";
            string itemurl = urls.Length > 1 ? urls[1] : "";
            string ballurl = urls.Length > 2 ? urls[2] : "";
            string teraurl = urls.Length > 3 ? urls[3] : "";
            string teraoriginurl = urls.Length > 4 ? urls[4] : "";
            string shinyurl = urls.Length > 5 ? urls[5] : "";

            // 标题: 昵称行
            var nameLine = lines.Length > 0 ? lines[0].Replace("**", "").Replace("昵称：", "").Trim() : "宝可梦";
            card.AddModule(new HeaderModuleBuilder().WithText(nameLine));

            // 主信息 + 宝可梦图
            var mes = string.Join("\n", lines.Take(7));
            if (lines.Length > 16)
                mes += "\n" + lines[16];
            var section = new SectionModuleBuilder().WithText(KMarkdown(mes));
            if (IsValidImageUrl(pokeurl))
                section.WithAccessory(Image(pokeurl!));
            card.AddModule(section);

            // 个体
            if (lines.Length > 8)
            {
                card.AddModule(new SectionModuleBuilder().WithText(KMarkdown("**个体：**")));
                card.AddModule(BuildStatParagraph(lines[8]));
            }
            // 努力
            if (lines.Length > 10)
            {
                card.AddModule(new SectionModuleBuilder().WithText(KMarkdown("**努力：**")));
                card.AddModule(BuildStatParagraph(lines[10]));
            }
            // 技能
            if (lines.Length > 11)
            {
                card.AddModule(new SectionModuleBuilder().WithText(KMarkdown("**技能：**")));
                for (int i = 0; i < 4; i++)
                {
                    if (lines.Length <= 12 + i) break;
                    var move = lines[12 + i].Trim();
                    if (string.IsNullOrEmpty(move)) continue;
                    var ms = new SectionModuleBuilder().WithText(KMarkdown($"**{move}**"));
                    var typeUrl = urls.Length > 6 + i ? urls[6 + i] : "";
                    if (IsValidImageUrl(typeUrl))
                        ms.WithAccessory(Image(typeUrl!));
                    card.AddModule(ms);
                }
            }

            // 底部图标行(闪光/球种/道具/太晶)
            var context = new ContextModuleBuilder();
            bool hasIcon = false;
            if (!string.IsNullOrEmpty(shinyurl)) { context.AddElement(PlainText(shinyurl!)); hasIcon = true; }
            if (IsValidImageUrl(ballurl)) { context.AddElement(Image(ballurl!)); hasIcon = true; }
            if (IsValidImageUrl(itemurl)) { context.AddElement(Image(itemurl!)); hasIcon = true; }
            if (IsValidImageUrl(teraurl)) { context.AddElement(Image(teraurl!)); hasIcon = true; }
            if (IsValidImageUrl(teraoriginurl)) { context.AddElement(Image(teraoriginurl!)); hasIcon = true; }
            if (hasIcon)
                card.AddModule(context);

            return card;
        }

        /// <summary>
        /// 蛋卡片
        /// </summary>
        private static CardBuilder BuildEggCard(string title, string message, string pokeurl, string ballurl, string shinyurl, string shinyinfo)
        {
            var card = new CardBuilder { Theme = CardTheme.None, Size = CardSize.Large };
            if (string.IsNullOrEmpty(title)) title = "宝可梦蛋";
            card.AddModule(new HeaderModuleBuilder().WithText(title.Replace("**", "")));

            if (string.IsNullOrEmpty(message)) message = "None";
            var lines = message.Split('\n');
            var mes = string.Join("\n", lines.Take(4));
            if (!string.IsNullOrEmpty(shinyinfo))
                mes += "\n" + shinyinfo;

            var section = new SectionModuleBuilder().WithText(KMarkdown(mes));
            if (IsValidImageUrl(pokeurl))
                section.WithAccessory(Image(pokeurl!));
            card.AddModule(section);

            if (lines.Length > 8)
            {
                card.AddModule(new SectionModuleBuilder().WithText(KMarkdown("**个体：**")));
                card.AddModule(BuildStatParagraph(lines[8]));
            }

            var context = new ContextModuleBuilder();
            bool hasIcon = false;
            if (!string.IsNullOrEmpty(shinyurl)) { context.AddElement(PlainText(shinyurl!)); hasIcon = true; }
            if (IsValidImageUrl(ballurl)) { context.AddElement(Image(ballurl!)); hasIcon = true; }
            if (hasIcon)
                card.AddModule(context);

            return card;
        }

        /// <summary>
        /// 批量卡片
        /// </summary>
        private static CardBuilder BuildBatchCard(string message, string pokeurl, string itemurl, string ballurl, string shinyurl)
        {
            var card = new CardBuilder { Theme = CardTheme.None, Size = CardSize.Large };
            card.AddModule(new HeaderModuleBuilder().WithText("批量交换"));

            if (string.IsNullOrEmpty(message)) message = "None";
            var lines = message.Split('\n');
            var first = lines.Length > 0 ? lines[0].Replace("**", "").Trim() : "";

            var section = new SectionModuleBuilder().WithText(KMarkdown(string.IsNullOrEmpty(first) ? "宝可梦" : first));
            if (IsValidImageUrl(pokeurl))
                section.WithAccessory(Image(pokeurl!));
            card.AddModule(section);

            var context = new ContextModuleBuilder();
            bool hasIcon = false;
            if (!string.IsNullOrEmpty(shinyurl)) { context.AddElement(PlainText(shinyurl!)); hasIcon = true; }
            if (IsValidImageUrl(ballurl)) { context.AddElement(Image(ballurl!)); hasIcon = true; }
            if (IsValidImageUrl(itemurl)) { context.AddElement(Image(itemurl!)); hasIcon = true; }
            if (hasIcon)
                card.AddModule(context);

            return card;
        }

        /// <summary>
        /// 个体/努力值段落: "HP :31,Atk:31,..." -> 3列2行
        /// </summary>
        private static SectionModuleBuilder BuildStatParagraph(string statLine)
        {
            var parts = statLine.Split(',');
            var paragraph = new ParagraphStructBuilder().WithColumnCount(3);
            foreach (var part in parts)
            {
                var field = part.Trim().Replace(":", "：");
                paragraph.AddField(KMarkdown(field));
            }
            return new SectionModuleBuilder().WithText(paragraph);
        }
    }
}
