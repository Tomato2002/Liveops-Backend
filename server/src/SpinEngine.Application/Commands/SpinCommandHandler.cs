using System;
using System.Threading;
using System.Threading.Tasks;
using SpinEngine.Application.Interfaces;
using SpinEngine.Domain.Services;
using SpinEngine.Shared.Contracts;

namespace SpinEngine.Application.Commands
{
    public class SpinCommand
    {
        public string PlayerId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public SpinRequest Request { get; set; } = new SpinRequest();
    }

    /// <summary>
    /// Điều phối ca sử dụng (Use Case) quay Slot:
    /// 1. Kiểm tra Idempotency Key (chống gửi trùng do lag mạng).
    /// 2. Bọc trong Distributed Lock (chống hack double-spending).
    /// 3. Khấu trừ tiền cược từ Wallet.
    /// 4. Kích hoạt RNG sinh điểm dừng và ma trận biểu tượng.
    /// 5. Đánh giá Paytable và Scatter.
    /// 6. Cộng tiền thắng và cập nhật Level vào Wallet.
    /// 7. Trả về SpinResult kèm CommonDto.
    /// </summary>
    public class SpinCommandHandler
    {
        private readonly IWalletRepository _walletRepo;
        private readonly ISlotConfigProvider _configProvider;
        private readonly ISlotRngService _rngService;
        private readonly IPaytableEvaluator _evaluator;
        private readonly IDistributedLockService _lockService;
        private readonly IIdempotencyStore _idempotencyStore;

        public SpinCommandHandler(
            IWalletRepository walletRepo,
            ISlotConfigProvider configProvider,
            ISlotRngService rngService,
            IPaytableEvaluator evaluator,
            IDistributedLockService lockService,
            IIdempotencyStore idempotencyStore)
        {
            _walletRepo = walletRepo;
            _configProvider = configProvider;
            _rngService = rngService;
            _evaluator = evaluator;
            _lockService = lockService;
            _idempotencyStore = idempotencyStore;
        }

        public async Task<SpinResult> HandleAsync(SpinCommand command, CancellationToken ct = default)
        {
            // 1. Kiểm tra Idempotency
            if (!string.IsNullOrWhiteSpace(command.RequestId))
            {
                var cached = await _idempotencyStore.GetCachedResponseAsync(command.RequestId, ct);
                if (cached != null)
                {
                    return cached;
                }
            }

            // 2. Acquire Distributed Lock theo tài khoản ví người chơi
            string lockKey = $"lock:wallet:{command.PlayerId}";
            await using var lockHandle = await _lockService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(5), ct);
            if (!lockHandle.IsAcquired)
            {
                throw new InvalidOperationException("Hệ thống đang xử lý giao dịch trước đó của bạn, vui lòng thử lại.");
            }

            // 3. Tải ví và cấu hình máy
            var wallet = await _walletRepo.GetWalletAsync(command.PlayerId, ct);
            var config = _configProvider.GetConfiguration(command.Request.GameId);

            bool isFreeSpin = !string.IsNullOrWhiteSpace(command.Request.FreeSpinRemainingToken);

            // 4. Khấu trừ cược nếu không phải Free Spin
            if (!isFreeSpin)
            {
                wallet.DeductCredits(command.Request.FullWager);
            }

            // 5. Sinh RNG
            int[] stopIndices = _rngService.GenerateStopIndices(config);
            string[][] visibleMatrix = _rngService.BuildVisibleMatrix(config, stopIndices);

            // 6. Đánh giá Paytable
            var eval = _evaluator.Evaluate(visibleMatrix, config, command.Request.FullWager, command.Request.ClientPaylines);

            // 7. Cộng thưởng
            if (eval.TotalWin > 0)
            {
                wallet.AddCredits(eval.TotalWin);
            }

            // 8. Lưu trạng thái ví
            await _walletRepo.SaveWalletAsync(wallet, ct);

            // 9. Đóng gói kết quả
            var spinResult = new SpinResult
            {
                RoundId = Guid.NewGuid().ToString("N"),
                StopIndices = stopIndices,
                VisibleMatrix = visibleMatrix,
                TotalWin = eval.TotalWin,
                PaylineWins = eval.PaylineWins,
                IsFreeSpinTriggered = eval.IsFreeSpinTriggered,
                FreeSpinsGranted = eval.FreeSpinsGranted,
                RemainingFreeSpins = isFreeSpin ? 0 : eval.FreeSpinsGranted,
                NextFreeSpinToken = eval.IsFreeSpinTriggered ? Guid.NewGuid().ToString("N") : null,
                CommonDto = wallet.ToCommonDto()
            };

            // 10. Lưu kết quả Idempotency
            if (!string.IsNullOrWhiteSpace(command.RequestId))
            {
                await _idempotencyStore.SaveResponseAsync(command.RequestId, spinResult, TimeSpan.FromMinutes(2), ct);
            }

            return spinResult;
        }
    }
}
