using Newtonsoft.Json;
using System.Collections.Generic;

namespace AutoTrader.Core.DTOs.Responses.TradeRanking
{
    public class TradeRankingResponse
    {
        [JsonProperty("rt_cd")]
        public string RtCd { get; set; } = string.Empty;

        [JsonProperty("msg_cd")]
        public string MsgCd { get; set; } = string.Empty;

        [JsonProperty("msg1")]
        public string Msg1 { get; set; } = string.Empty;

        [JsonProperty("output1")]
        public TradeRankingMeta Output1 { get; set; } = new TradeRankingMeta();

        [JsonProperty("output2")]
        public List<TradeRankingItem> Output2 { get; set; } = new List<TradeRankingItem>();
    }

    public class TradeRankingMeta
    {
        [JsonProperty("crec")]
        public string CurrentRecordCount { get; set; } = string.Empty;
    }

    public class TradeRankingItem
    {
        [JsonProperty("symb")]
        public string Symbol { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("last")]
        public decimal CurrentPrice { get; set; }

        [JsonProperty("rate")]
        public decimal ChangePercent { get; set; }

        [JsonProperty("tvol")]
        public decimal TradeVolume { get; set; }

        [JsonProperty("tamt")]
        public decimal TradeAmount { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }
    }
}
