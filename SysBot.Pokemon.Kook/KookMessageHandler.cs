using Kook;
using Kook.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Kook
{
    /// <summary>
    /// Kook 频道消息处理(移植自 Dodo 平台的 PokemonProcessService)
    /// </summary>
    public static class KookMessageHandler<T> where T : PKM, new()
    {
        private static readonly string LogIdentity = "KookBot";
        private static readonly string Welcome = "宝可梦机器人为您服务\n中文指令请看在线文件:https://docs.qq.com/doc/DVWdQdXJPWllabm5t?&u=1c5a2618155548239a9563e9f22a57c0\n或者使用PS代码\n或者上传pk文件\n取消排队请输入:取消\n当前位置请输入:位置";

        private static uint Count;

        public static async Task HandleMessage(SocketMessage msg, SocketGuildUser user, SocketTextChannel channel)
        {
            // 机器人自己不响应
            if (msg.Author.Id == KookBot<T>.BotUserId)
                return;

            var settings = KookBot<T>.Info.Hub.Config.Kook;
            if (!ulong.TryParse(settings.ChannelId, out var channelId) || channel.Id != channelId)
                return;

            if (Count > 100)
                Count = 0;

            var vip = HasRole(user, settings.VipRole);
            var batch = HasRole(user, settings.BatchRole);
            var userId = user.Id;

            // ---------- 文件交换 ----------
            if (msg.Type == MessageType.File && msg.Attachments.Count > 0)
            {
                var file = msg.Attachments.First();
                var fileName = file.Filename;

                // 大队长文件检测(文件名以2022/2023/2024日期开头)
                if (fileName.Length > 8 && Regex.IsMatch(fileName.Substring(0, 8), "^(2022|2023|2024)(?<month>\\d{2})(?<day>\\d{2})$"))
                {
                    await KookBot<T>.SendChannelMessage("**大队长与狗不得使用**", channel.Id);
                    LogUtil.LogInfo($"用户 {user.Username}({userId}) 使用了大队长的文件, 已警告", LogIdentity);
                    return;
                }

                if (!FileTradeHelper<T>.IsValidFileSize(file.Size ?? 0) || !FileTradeHelper<T>.IsValidFileName(fileName))
                {
                    await Withdraw(msg, channel);
                    await KookBot<T>.SendChannelMessage("非法文件", channel.Id);
                    LogUtil.LogInfo($"用户 {user.Username}({userId}) 使用非法文件, 已警告", LogIdentity);
                    return;
                }

                byte[] downloadBytes;
                using (var client = new HttpClient())
                    downloadBytes = await client.GetByteArrayAsync(file.Url);

                var pkms = FileTradeHelper<T>.DataToList(downloadBytes);
                await Withdraw(msg, channel);
                if (pkms.Count == 1)
                {
                    if (vip)
                    {
                        await KookBot<T>.SendChannelAtMessage(userId, "尊贵的VIP用户,请走VIP通道", channel.Id);
                        new KookHelper<T>(userId, user.Username, channel.Id).StartTradePKM(pkms[0], true, Count);
                        Count++;
                    }
                    else
                    {
                        new KookHelper<T>(userId, user.Username, channel.Id).StartTradePKM(pkms[0]);
                    }
                }
                else if (pkms.Count > 1 && pkms.Count <= FileTradeHelper<T>.maxPokemonCountInBin)
                {
                    if (!batch && !vip)
                    {
                        await KookBot<T>.SendChannelMessage("你没有批量权限", channel.Id);
                    }
                    else
                    {
                        if (vip)
                        {
                            await KookBot<T>.SendChannelAtMessage(userId, "尊贵的VIP用户,请走VIP通道", channel.Id);
                            new KookHelper<T>(userId, user.Username, channel.Id).StartTradeMultiPKM(pkms, userId.ToString(), true, Count);
                            Count++;
                        }
                        else
                        {
                            new KookHelper<T>(userId, user.Username, channel.Id).StartTradeMultiPKM(pkms, userId.ToString());
                        }
                    }
                }
                else
                {
                    await KookBot<T>.SendChannelMessage("文件内容不正确", channel.Id);
                }
                return;
            }

            // ---------- 指令交换 ----------
            if (msg.Type != MessageType.Text && msg.Type != MessageType.KMarkdown)
                return;

            var content = msg.Content;
            LogUtil.LogInfo($"{user.Username}({userId}):{content}", LogIdentity);

            // 只有 @机器人 的消息才会被处理
            var mention = $"(met){KookBot<T>.BotUserId}(met)";
            if (!content.Contains(mention))
                return;
            content = content.Replace(mention, "").Trim();

            // 批量PS(多条PS代码以空行分隔)
            if (ShowdownTranslator<T>.IsPS(content) && content.Contains("\n\n"))
            {
                await Withdraw(msg, channel);
                if (!batch && !vip)
                {
                    await KookBot<T>.SendChannelMessage("你没有批量权限", channel.Id);
                }
                else
                {
                    if (vip)
                    {
                        await KookBot<T>.SendChannelAtMessage(userId, "尊贵的VIP用户,请走VIP通道", channel.Id);
                        new KookHelper<T>(userId, user.Username, channel.Id).StartTradeMultiPs(content, userId.ToString(), true, Count);
                        Count++;
                    }
                    else
                    {
                        new KookHelper<T>(userId, user.Username, channel.Id).StartTradeMultiPs(content, userId.ToString());
                    }
                }
                return;
            }

            // 单条PS
            if (ShowdownTranslator<T>.IsPS(content))
            {
                await Withdraw(msg, channel);
                if (vip)
                {
                    await KookBot<T>.SendChannelAtMessage(userId, "尊贵的VIP用户,请走VIP通道", channel.Id);
                    new KookHelper<T>(userId, user.Username, channel.Id).StartTradePs(content, true, Count);
                    Count++;
                }
                else
                {
                    new KookHelper<T>(userId, user.Username, channel.Id).StartTradePs(content);
                }
                return;
            }

            // 检测(交换后检测宝可梦)
            if (content.StartsWith("检测"))
            {
                await Withdraw(msg, channel);
                new KookHelper<T>(userId, user.Username, channel.Id).StartDump();
                return;
            }

            // 批量中文指令(以+分隔)
            if (content.Contains('+'))
            {
                await Withdraw(msg, channel);
                if (!vip && !batch)
                {
                    await KookBot<T>.SendChannelMessage("你没有批量权限", channel.Id);
                }
                else
                {
                    if (vip)
                    {
                        await KookBot<T>.SendChannelAtMessage(userId, "尊贵的VIP用户,请走VIP通道", channel.Id);
                        new KookHelper<T>(userId, user.Username, channel.Id).StartTradeMultiChinesePs(content, userId.ToString(), true, Count);
                        Count++;
                    }
                    else
                    {
                        new KookHelper<T>(userId, user.Username, channel.Id).StartTradeMultiChinesePs(content, userId.ToString());
                    }
                }
                return;
            }

            // 取消排队
            if (content.Contains("取消"))
            {
                var result = KookBot<T>.Info.ClearTrade(userId);
                await KookBot<T>.SendChannelAtMessage(userId, $" {GetClearTradeMessage(result)}", channel.Id);
                return;
            }

            // 当前位置
            if (content.Contains("位置"))
            {
                var result = KookBot<T>.Info.CheckPosition(userId);
                await KookBot<T>.SendChannelAtMessage(userId, $" {GetQueueCheckResultMessage(result)}", channel.Id);
                return;
            }

            // 中文指令
            var ps = content;
            await Withdraw(msg, channel);
            if (!string.IsNullOrWhiteSpace(ps))
            {
                if (ps.Trim() == "取消" || ps.Trim() == "位置")
                    return;
                LogUtil.LogInfo($"收到命令\n{ps}", LogIdentity);
                if (vip)
                {
                    await KookBot<T>.SendChannelAtMessage(userId, "尊贵的VIP用户,请走VIP通道", channel.Id);
                    new KookHelper<T>(userId, user.Username, channel.Id).StartTradeChinesePs(ps, true, Count);
                    Count++;
                }
                else
                {
                    new KookHelper<T>(userId, user.Username, channel.Id).StartTradeChinesePs(ps);
                }
            }
            else
            {
                await KookBot<T>.SendChannelMessage($"{Welcome}", channel.Id);
            }
        }

        private static bool HasRole(SocketGuildUser user, string roleSetting)
        {
            if (string.IsNullOrWhiteSpace(roleSetting) || !ulong.TryParse(roleSetting, out var roleId))
                return false;
            return user.Roles.Any(r => (ulong)r.Id == roleId);
        }

        public static string GetQueueCheckResultMessage(QueueCheckResult<T> result)
        {
            if (!result.InQueue || result.Detail is null)
                return "你目前不在队列里";
            var msg = $"你在第***{result.Position}位***";
            var pk = result.Detail.Trade.TradeData;
            if (pk.Species != 0)
                msg += $"，交换宝可梦：***{ShowdownTranslator<T>.GameStringsZh.Species[result.Detail.Trade.TradeData.Species]}***";
            return msg;
        }

        private static string GetClearTradeMessage(QueueResultRemove result)
        {
            return result switch
            {
                QueueResultRemove.CurrentlyProcessing => "你正在交换队列中",
                QueueResultRemove.CurrentlyProcessingRemoved => "正在从队列中删除",
                QueueResultRemove.Removed => "已从队列中删除",
                _ => "你已经不在队列里",
            };
        }

        private static async Task Withdraw(SocketMessage msg, SocketTextChannel channel)
        {
            if (KookBot<T>.Info.Hub.Config.Kook.WithdrawTradeMessage)
                await channel.DeleteMessageAsync(msg.Id);
        }
    }
}
