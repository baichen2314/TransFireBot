using PKHeX.Core;
using SysBot.Pokemon.Helpers;

namespace SysBot.Pokemon.Kook
{
    public class KookHelper<T> : PokemonTradeHelper<T> where T : PKM, new()
    {
        private readonly ulong userId;
        private readonly ulong channelId;

        public KookHelper(ulong userId, string nickName, ulong channelId)
        {
            SetPokeTradeTrainerInfo(new PokeTradeTrainerInfo(nickName, userId));
            SetTradeQueueInfo(KookBot<T>.Info);
            this.userId = userId;
            this.channelId = channelId;
        }

        public override IPokeTradeNotifier<T> GetPokeTradeNotifier(T pkm, int code)
        {
            return new KookTradeNotifier<T>(pkm, userInfo, code, userInfo.ID.ToString(), channelId);
        }

        public override void SendMessage(string message)
        {
            _ = KookBot<T>.SendChannelAtMessage(userId, message, channelId);
        }

        public override void SendCardMessage(string message, string pokeurl, string itemurl, string ballurl, string teraurl, string teraoriginalurl, string shinyurl, string movetypeurl1, string movetypeurl2, string movetypeurl3, string movetypeurl4)
        {
            _ = KookBot<T>.SendChannelCardMessage(message, channelId, pokeurl, itemurl, ballurl, teraurl, teraoriginalurl, shinyurl, movetypeurl1, movetypeurl2, movetypeurl3, movetypeurl4);
        }

        public override void SendCardBatchMessage(string message, string pokeurl, string itemurl, string ballurl, string shinyurl)
        {
            _ = KookBot<T>.SendChannelCardBatchMessage(message, channelId, pokeurl, itemurl, ballurl, shinyurl);
        }
    }
}
