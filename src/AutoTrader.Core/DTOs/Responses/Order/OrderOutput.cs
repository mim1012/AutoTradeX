using Newtonsoft.Json;

namespace AutoTrader.Core.DTOs.Responses.Order;

/// <summary>
/// ü8 °ü ô
/// </summary>
public class OrderOutput
{
    /// <summary>
    /// ü8ˆ8 (ü8 ”© à  ˆ8)
    /// </summary>
    [JsonProperty("ODNO")]
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// ü8Ü (HHMMSS Ý)
    /// : "153045" ’ 15:30:45
    /// </summary>
    [JsonProperty("ORD_TMD")]
    public string OrderTime { get; set; } = string.Empty;

    /// <summary>
    /// ü8Ü ñ (TimeSpan)
    /// </summary>
    [JsonIgnore]
    public TimeSpan? OrderTimeSpan
    {
        get
        {
            if (string.IsNullOrEmpty(OrderTime) || OrderTime.Length != 6)
                return null;

            if (int.TryParse(OrderTime.Substring(0, 2), out int hours) &&
                int.TryParse(OrderTime.Substring(2, 2), out int minutes) &&
                int.TryParse(OrderTime.Substring(4, 2), out int seconds))
            {
                return new TimeSpan(hours, minutes, seconds);
            }

            return null;
        }
    }
}
