using System;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon
{
    public class KookSettings
    {
        private const string Startup = nameof(Startup);

        public override string ToString() => "Kook整合设置";

        // Startup
        [Category(Startup), Description("机器人鉴权Token")]
        public string Token { get; set; } = string.Empty;

        [Category(Startup), Description("机器人响应频道id")]
        public string ChannelId { get; set; } = string.Empty;

        [Category(Startup), Description("可以插队的身份组")]
        public string VipRole { get; set; } = "1111111";

        [Category(Startup), Description("可以批量的身份组")]
        public string BatchRole { get; set; } = "1111111";

        [Category(Startup), Description("是否撤回交换消息")]
        public bool WithdrawTradeMessage { get; set; } = false;

        [Category(Startup), Description("是否开启卡片消息")]
        public bool CardTradeMessage { get; set; } = true;

        [Category(Startup), Description("是否将宝可梦文件私发给用户")]
        public bool ReturnPKMs { get; set; } = false;
    }
}