using System;
using System.Collections.Generic;

namespace SpinEngine.Domain.Entities
{
    public class Dimensions
    {
        public int Reels { get; set; } = 5;
        public int Rows { get; set; } = 3;
    }

    public class SymbolDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsWild { get; set; }
        public bool IsScatter { get; set; }
    }

    public class ScatterReward
    {
        public int FreeSpins { get; set; }
        public int Multiplier { get; set; }
    }

    public class ScatterRule
    {
        public string SymbolId { get; set; } = "SCATTER";
        public int TriggerMinCount { get; set; } = 3;
        public Dictionary<string, ScatterReward> Rewards { get; set; } = new Dictionary<string, ScatterReward>();
    }

    /// <summary>
    /// Thực thể cấu hình của một máy Slot, được load từ file JSON cấu hình.
    /// Toàn bộ thông số toán học (Paytable, Reels, Paylines) đều nằm ở đây.
    /// </summary>
    public class SlotConfiguration
    {
        public string GameId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public Dimensions Dimensions { get; set; } = new Dimensions();
        public List<long> BetTiers { get; set; } = new List<long>();
        public List<SymbolDefinition> Symbols { get; set; } = new List<SymbolDefinition>();
        
        /// <summary>
        /// Paytable: [SymbolId] -> [MatchCount (3,4,5)] -> Multiplier
        /// </summary>
        public Dictionary<string, Dictionary<string, int>> Paytable { get; set; } 
            = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

        public ScatterRule ScatterRules { get; set; } = new ScatterRule();

        /// <summary>
        /// Danh sách các payline: Mỗi payline là mảng các số hàng (row index) trên từng cuộn.
        /// Ví dụ: [1, 1, 1, 1, 1] là đường thẳng ở hàng giữa.
        /// </summary>
        public List<int[]> Paylines { get; set; } = new List<int[]>();

        /// <summary>
        /// Dải cuộn của từng cột: [ReelIndex "0".."4"] -> Mảng các SymbolId
        /// </summary>
        public Dictionary<string, string[]> ReelStrips { get; set; } 
            = new Dictionary<string, string[]>();
    }
}
