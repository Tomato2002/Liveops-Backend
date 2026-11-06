using System;
using System.Collections.Generic;
using SpinEngine.Domain.Entities;
using SpinEngine.Domain.Services;
using Xunit;

namespace SpinEngine.Tests
{
    public class WalletTests
    {
        [Fact]
        public void DeductCredits_WithSufficientBalance_ShouldDeductAndAddExp()
        {
            var wallet = new Wallet("player1", initialCredits: 10000);
            wallet.DeductCredits(2000);

            Assert.Equal(8000, wallet.Credits);
            Assert.Equal(200, wallet.Experience); // 2000 / 10
        }

        [Fact]
        public void DeductCredits_WithInsufficientBalance_ShouldThrowException()
        {
            var wallet = new Wallet("player2", initialCredits: 500);

            Assert.Throws<InsufficientFundsException>(() =>
            {
                wallet.DeductCredits(1000);
            });
        }

        [Fact]
        public void AddCredits_ShouldIncreaseBalance()
        {
            var wallet = new Wallet("player3", initialCredits: 1000);
            wallet.AddCredits(5000);

            Assert.Equal(6000, wallet.Credits);
        }
    }

    public class PaytableEvaluatorTests
    {
        private SlotConfiguration CreateTestConfig()
        {
            return new SlotConfiguration
            {
                GameId = "test_slot",
                Dimensions = new Dimensions { Reels = 5, Rows = 3 },
                Paytable = new Dictionary<string, Dictionary<string, int>>
                {
                    ["SEVEN"] = new Dictionary<string, int> { ["3"] = 50, ["4"] = 200, ["5"] = 1000 },
                    ["CHERRY"] = new Dictionary<string, int> { ["3"] = 10, ["4"] = 25, ["5"] = 100 }
                },
                ScatterRules = new ScatterRule
                {
                    SymbolId = "SCATTER",
                    TriggerMinCount = 3,
                    Rewards = new Dictionary<string, ScatterReward>
                    {
                        ["3"] = new ScatterReward { FreeSpins = 10, Multiplier = 2 }
                    }
                },
                Paylines = new List<int[]>
                {
                    new int[] { 1, 1, 1, 1, 1 }, // Hàng giữa
                    new int[] { 0, 0, 0, 0, 0 }  // Hàng trên
                }
            };
        }

        [Fact]
        public void Evaluate_ThreeSevensOnLine_ShouldWinFiftyTimesLineBet()
        {
            var evaluator = new PaytableEvaluator();
            var config = CreateTestConfig();

            // Ma trận 5 cột x 3 hàng: Cột 0, 1, 2 có SEVEN ở hàng giữa (row 1)
            string[][] matrix = new string[][]
            {
                new[] { "LEMON", "SEVEN", "BAR" },
                new[] { "LEMON", "SEVEN", "BAR" },
                new[] { "LEMON", "SEVEN", "BAR" },
                new[] { "LEMON", "BELL", "BAR" },
                new[] { "LEMON", "BELL", "BAR" },
            };

            long wager = 200; // 2 dòng active -> 100/dòng
            var result = evaluator.Evaluate(matrix, config, fullWager: wager, activePaylines: 2);

            Assert.Single(result.PaylineWins);
            var win = result.PaylineWins[0];
            Assert.Equal(0, win.PaylineIndex);
            Assert.Equal("SEVEN", win.SymbolId);
            Assert.Equal(3, win.MatchCount);
            Assert.Equal(50, win.WinMultiplier);
            Assert.Equal(5000, win.WinAmount); // 100 * 50
            Assert.Equal(5000, result.TotalWin);
            Assert.False(result.IsFreeSpinTriggered);
        }

        [Fact]
        public void Evaluate_WildSubstitute_ShouldMatchAsTargetSymbol()
        {
            var evaluator = new PaytableEvaluator();
            var config = CreateTestConfig();

            // Cột 0: WILD, Cột 1: SEVEN, Cột 2: SEVEN ở hàng 1
            string[][] matrix = new string[][]
            {
                new[] { "LEMON", "WILD", "BAR" },
                new[] { "LEMON", "SEVEN", "BAR" },
                new[] { "LEMON", "SEVEN", "BAR" },
                new[] { "LEMON", "BELL", "BAR" },
                new[] { "LEMON", "BELL", "BAR" },
            };

            long wager = 200;
            var result = evaluator.Evaluate(matrix, config, fullWager: wager, activePaylines: 2);

            Assert.Single(result.PaylineWins);
            Assert.Equal("SEVEN", result.PaylineWins[0].SymbolId);
            Assert.Equal(3, result.PaylineWins[0].MatchCount);
        }

        [Fact]
        public void Evaluate_ThreeScatters_ShouldTriggerFreeSpins()
        {
            var evaluator = new PaytableEvaluator();
            var config = CreateTestConfig();

            // 3 Scatter ở bất kỳ đâu
            string[][] matrix = new string[][]
            {
                new[] { "SCATTER", "LEMON", "BAR" },
                new[] { "LEMON", "SCATTER", "BAR" },
                new[] { "LEMON", "BAR", "SCATTER" },
                new[] { "LEMON", "BELL", "BAR" },
                new[] { "LEMON", "BELL", "BAR" },
            };

            long wager = 100;
            var result = evaluator.Evaluate(matrix, config, fullWager: wager, activePaylines: 1);

            Assert.True(result.IsFreeSpinTriggered);
            Assert.Equal(10, result.FreeSpinsGranted);
            Assert.Equal(200, result.TotalWin); // Multiplier 2 * 100 wager
        }
    }
}
