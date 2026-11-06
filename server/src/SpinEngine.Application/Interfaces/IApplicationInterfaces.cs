using System;
using System.Threading;
using System.Threading.Tasks;
using SpinEngine.Domain.Entities;
using SpinEngine.Shared.Contracts;

namespace SpinEngine.Application.Interfaces
{
    public interface IWalletRepository
    {
        Task<Wallet> GetWalletAsync(string playerId, CancellationToken cancellationToken = default);
        Task SaveWalletAsync(Wallet wallet, CancellationToken cancellationToken = default);
    }

    public interface ISlotConfigProvider
    {
        SlotConfiguration GetConfiguration(string gameId);
    }

    public interface IDistributedLockHandle : IAsyncDisposable
    {
        bool IsAcquired { get; }
    }

    public interface IDistributedLockService
    {
        /// <summary>
        /// Khóa phân tán (Distributed Lock) dựa trên Redis.
        /// Chống tấn công click đúp / double spending cùng 1 tài khoản ví.
        /// </summary>
        Task<IDistributedLockHandle> AcquireLockAsync(string resourceKey, TimeSpan timeout, CancellationToken cancellationToken = default);
    }

    public interface IIdempotencyStore
    {
        /// <summary>
        /// Tra cứu kết quả ván chơi đã xử lý từ trước với Idempotency Key (X-Request-Id).
        /// </summary>
        Task<SpinResult?> GetCachedResponseAsync(string requestId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lưu kết quả ván chơi vào cache để nếu mạng lag gửi lại cùng RequestId thì trả về ngay.
        /// </summary>
        Task SaveResponseAsync(string requestId, SpinResult result, TimeSpan ttl, CancellationToken cancellationToken = default);
    }
}
