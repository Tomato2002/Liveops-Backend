using System;
using System.Collections.Generic;
using System.Linq;
using SpinEngine.Domain.Entities;
using SpinEngine.Shared.Contracts;

namespace SpinEngine.Domain.Services
{
    public class EvaluationResult
    {
        public long TotalWin { get; set; }
        public List<PaylineWinInfo> PaylineWins { get; set; } = new List<PaylineWinInfo>();
        public bool IsFreeSpinTriggered { get; set; }
        public int FreeSpinsGranted { get; set; }
    }

    public interface IPaytableEvaluator
    {
        /// <summary>
        /// Đánh giá ma trận biểu tượng để tính toán tiền thắng và các tính năng kích hoạt.
        /// </summary>
        EvaluationResult Evaluate(string[][] visibleMatrix, SlotConfiguration config, long fullWager, int activePaylines);
    }

    /// <summary>
    /// Bộ tính toán trả thưởng Paytable thuần túy (Domain Service).
    /// Hỗ trợ Wild substitution (thay thế biểu tượng) và Scatter triggering.
    /// </summary>
    public class PaytableEvaluator : IPaytableEvaluator
    {
        private const string WILD_SYMBOL = "WILD";
        private const string SCATTER_SYMBOL = "SCATTER";

        public EvaluationResult Evaluate(string[][] visibleMatrix, SlotConfiguration config, long fullWager, int activePaylines)
        {
            var result = new EvaluationResult();
            if (activePaylines <= 0) return result;

            long betPerLine = Math.Max(1, fullWager / activePaylines);
            int linesToEvaluate = Math.Min(activePaylines, config.Paylines.Count);

            // 1. Đánh giá từng Payline từ Trái sang Phải (Left-to-Right)
            for (int lineIndex = 0; lineIndex < linesToEvaluate; lineIndex++)
            {
                int[] lineDef = config.Paylines[lineIndex];
                var winInfo = EvaluateSingleLine(visibleMatrix, lineDef, lineIndex, config, betPerLine);
                if (winInfo != null)
                {
                    result.PaylineWins.Add(winInfo);
                    result.TotalWin += winInfo.WinAmount;
                }
            }

            // 2. Đánh giá biểu tượng Scatter (Tính năng Free Spins)
            EvaluateScatters(visibleMatrix, config, fullWager, result);

            return result;
        }

        private PaylineWinInfo? EvaluateSingleLine(
            string[][] matrix, 
            int[] lineDef, 
            int lineIndex, 
            SlotConfiguration config, 
            long betPerLine)
        {
            int reelCount = matrix.Length;
            if (lineDef.Length < reelCount) return null;

            // Thu thập các symbol trên đường payline này
            string[] lineSymbols = new string[reelCount];
            for (int r = 0; r < reelCount; r++)
            {
                int row = lineDef[r];
                lineSymbols[r] = matrix[r][row];
            }

            // Xác định symbol mục tiêu (Target Symbol)
            // Tìm symbol không phải Wild và không phải Scatter đầu tiên
            string? targetSymbol = null;
            for (int r = 0; r < reelCount; r++)
            {
                if (lineSymbols[r] != WILD_SYMBOL && lineSymbols[r] != SCATTER_SYMBOL)
                {
                    targetSymbol = lineSymbols[r];
                    break;
                }
            }

            // Trường hợp toàn bộ dòng là WILD (5 con WILD)
            if (targetSymbol == null)
            {
                targetSymbol = "SEVEN"; // Biểu tượng cao nhất
            }

            // Đếm số lượng biểu tượng khớp liên tiếp từ cuộn 0
            int matchCount = 0;
            var winningPositions = new List<SymbolCoordinate>();

            for (int r = 0; r < reelCount; r++)
            {
                string sym = lineSymbols[r];
                if (sym == targetSymbol || sym == WILD_SYMBOL)
                {
                    matchCount++;
                    winningPositions.Add(new SymbolCoordinate(r, lineDef[r]));
                }
                else
                {
                    break; // Ngắt chuỗi liên tiếp
                }
            }

            // Trả thưởng yêu cầu tối thiểu 3 biểu tượng liên tiếp
            if (matchCount >= 3 && config.Paytable.TryGetValue(targetSymbol, out var symbolPayouts))
            {
                string matchKey = matchCount.ToString();
                if (symbolPayouts.TryGetValue(matchKey, out int multiplier))
                {
                    return new PaylineWinInfo
                    {
                        PaylineIndex = lineIndex,
                        SymbolId = targetSymbol,
                        MatchCount = matchCount,
                        WinMultiplier = multiplier,
                        WinAmount = betPerLine * multiplier,
                        WinningPositions = winningPositions
                    };
                }
            }

            return null;
        }

        private void EvaluateScatters(string[][] matrix, SlotConfiguration config, long fullWager, EvaluationResult result)
        {
            int scatterCount = 0;
            int reelCount = matrix.Length;

            for (int r = 0; r < reelCount; r++)
            {
                for (int row = 0; row < matrix[r].Length; row++)
                {
                    if (string.Equals(matrix[r][row], SCATTER_SYMBOL, StringComparison.OrdinalIgnoreCase))
                    {
                        scatterCount++;
                    }
                }
            }

            var rule = config.ScatterRules;
            if (scatterCount >= rule.TriggerMinCount)
            {
                result.IsFreeSpinTriggered = true;
                string countKey = scatterCount.ToString();

                // Lấy phần thưởng Free Spins tương ứng số Scatter trúng được
                if (rule.Rewards.TryGetValue(countKey, out var reward) || 
                    rule.Rewards.TryGetValue(rule.TriggerMinCount.ToString(), out reward))
                {
                    result.FreeSpinsGranted = reward.FreeSpins;
                    if (reward.Multiplier > 0)
                    {
                        long scatterWin = fullWager * reward.Multiplier;
                        result.TotalWin += scatterWin;
                    }
                }
            }
        }
    }
}
