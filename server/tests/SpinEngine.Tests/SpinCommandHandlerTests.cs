using System;
using System.IO;
using System.Threading.Tasks;
using SpinEngine.Application.Commands;
using SpinEngine.Domain.Services;
using SpinEngine.Infrastructure.Services;
using SpinEngine.Shared.Contracts;
using Xunit;

namespace SpinEngine.Tests
{
    public class SpinCommandHandlerTests
    {
        [Fact]
        public async Task Spin_EndToEnd_ShouldProcessSuccessfullyAndSyncBalance()
        {
            // Setup full pipeline
            var configDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../shared/configs"));
            var configProvider = new FileSlotConfigProvider(configDir);
            var walletRepo = new InMemoryWalletRepository();
            var rng = new SlotRngService();
            var evaluator = new PaytableEvaluator();
            var lockService = new InMemoryDistributedLockService();
            var idempotencyStore = new InMemoryIdempotencyStore();

            var handler = new SpinCommandHandler(
                walletRepo, configProvider, rng, evaluator, lockService, idempotencyStore);

            string playerId = "test_player_e2e";
            var initialWallet = await walletRepo.GetWalletAsync(playerId);
            long initialCredits = initialWallet.Credits;

            var command = new SpinCommand
            {
                PlayerId = playerId,
                RequestId = Guid.NewGuid().ToString("N"),
                Request = new SpinRequest
                {
                    GameId = "classic_slot_machine",
                    FullWager = 2000,
                    ClientPaylines = 20
                }
            };

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.StopIndices.Length);
            Assert.Equal(5, result.VisibleMatrix.Length);
            Assert.Equal(3, result.VisibleMatrix[0].Length);
            Assert.NotNull(result.CommonDto);

            // Kiểm tra số dư ví khớp chính xác: initial - wager + win
            long expectedBalance = initialCredits - 2000 + result.TotalWin;
            Assert.Equal(expectedBalance, result.CommonDto.Balances.Credits);

            var updatedWallet = await walletRepo.GetWalletAsync(playerId);
            Assert.Equal(expectedBalance, updatedWallet.Credits);
        }

        [Fact]
        public async Task Spin_WithDuplicateRequestId_ShouldReturnCachedResultWithoutDoubleSpending()
        {
            var configDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../shared/configs"));
            var configProvider = new FileSlotConfigProvider(configDir);
            var walletRepo = new InMemoryWalletRepository();
            var rng = new SlotRngService();
            var evaluator = new PaytableEvaluator();
            var lockService = new InMemoryDistributedLockService();
            var idempotencyStore = new InMemoryIdempotencyStore();

            var handler = new SpinCommandHandler(
                walletRepo, configProvider, rng, evaluator, lockService, idempotencyStore);

            string playerId = "test_player_idempotency";
            string fixedRequestId = "duplicate_request_guid_12345";

            var command = new SpinCommand
            {
                PlayerId = playerId,
                RequestId = fixedRequestId,
                Request = new SpinRequest
                {
                    GameId = "classic_slot_machine",
                    FullWager = 5000,
                    ClientPaylines = 20
                }
            };

            // Lần gửi 1
            var result1 = await handler.HandleAsync(command);
            long balanceAfterSpin1 = result1.CommonDto.Balances.Credits;

            // Lần gửi 2 (mạng bị lag hoặc gửi đúp cùng 1 RequestId)
            var result2 = await handler.HandleAsync(command);
            long balanceAfterSpin2 = result2.CommonDto.Balances.Credits;

            // Phải trả về cùng RoundId và tuyệt đối không trừ tiền lần 2!
            Assert.Equal(result1.RoundId, result2.RoundId);
            Assert.Equal(balanceAfterSpin1, balanceAfterSpin2);

            var wallet = await walletRepo.GetWalletAsync(playerId);
            Assert.Equal(balanceAfterSpin1, wallet.Credits);
        }
    }
}
