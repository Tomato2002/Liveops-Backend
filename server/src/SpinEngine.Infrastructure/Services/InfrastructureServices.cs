using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SpinEngine.Application.Interfaces;
using SpinEngine.Domain.Entities;
using SpinEngine.Shared.Contracts;

namespace SpinEngine.Infrastructure.Services
{
    /// <summary>
    /// Đọc cấu hình JSON máy Slot từ thư mục shared/configs/
    /// </summary>
    public class FileSlotConfigProvider : ISlotConfigProvider
    {
        private readonly string _configDirectory;
        private readonly ConcurrentDictionary<string, SlotConfiguration> _cache = new();

        public FileSlotConfigProvider(string? configDirectory = null)
        {
            // Mặc định trỏ tới shared/configs/
            _configDirectory = configDirectory ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../shared/configs"));
            if (!Directory.Exists(_configDirectory))
            {
                // Fallback nếu chạy trong môi trường release/docker
                _configDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "configs"));
            }
        }

        public SlotConfiguration GetConfiguration(string gameId)
        {
            return _cache.GetOrAdd(gameId, id =>
            {
                string filePath = Path.Combine(_configDirectory, $"{id}.json");
                if (!File.Exists(filePath))
                {
                    // Fallback to classic_slot_machine.json
                    filePath = Path.Combine(_configDirectory, "classic_slot_machine.json");
                }

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Không tìm thấy file cấu hình máy slot tại: {filePath}");
                }

                string json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var config = JsonSerializer.Deserialize<SlotConfiguration>(json, options);
                return config ?? throw new InvalidOperationException($"Lỗi phân tích JSON cấu hình máy: {filePath}");
            });
        }
    }

    /// <summary>
    /// Lưu trữ ví tiền trong RAM (hỗ trợ thread-safe concurrency cho môi trường dev/test).
    /// </summary>
    public class InMemoryWalletRepository : IWalletRepository
    {
        private readonly ConcurrentDictionary<string, Wallet> _wallets = new();

        public Task<Wallet> GetWalletAsync(string playerId, CancellationToken cancellationToken = default)
        {
            var wallet = _wallets.GetOrAdd(playerId, id => new Wallet(id, initialCredits: 10000000));
            return Task.FromResult(wallet);
        }

        public Task SaveWalletAsync(Wallet wallet, CancellationToken cancellationToken = default)
        {
            _wallets[wallet.PlayerId] = wallet;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Khóa phân tán giả lập trong RAM (cho môi trường không có Redis thật).
    /// </summary>
    public class InMemoryDistributedLockService : IDistributedLockService
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        private class LockHandle : IDistributedLockHandle
        {
            private readonly SemaphoreSlim _semaphore;
            private bool _disposed;

            public bool IsAcquired { get; }

            public LockHandle(SemaphoreSlim semaphore, bool isAcquired)
            {
                _semaphore = semaphore;
                IsAcquired = isAcquired;
            }

            public ValueTask DisposeAsync()
            {
                if (!_disposed && IsAcquired)
                {
                    _semaphore.Release();
                    _disposed = true;
                }
                return ValueTask.CompletedTask;
            }
        }

        public async Task<IDistributedLockHandle> AcquireLockAsync(string resourceKey, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var semaphore = _locks.GetOrAdd(resourceKey, _ => new SemaphoreSlim(1, 1));
            bool acquired = await semaphore.WaitAsync(timeout, cancellationToken);
            return new LockHandle(semaphore, acquired);
        }
    }

    /// <summary>
    /// Cache lưu trữ Idempotency Key (X-Request-Id) trong RAM.
    /// </summary>
    public class InMemoryIdempotencyStore : IIdempotencyStore
    {
        private class CacheEntry
        {
            public SpinResult Result { get; set; } = null!;
            public DateTime ExpiresAt { get; set; }
        }

        private readonly ConcurrentDictionary<string, CacheEntry> _store = new();

        public Task<SpinResult?> GetCachedResponseAsync(string requestId, CancellationToken cancellationToken = default)
        {
            if (_store.TryGetValue(requestId, out var entry))
            {
                if (DateTime.UtcNow <= entry.ExpiresAt)
                {
                    return Task.FromResult<SpinResult?>(entry.Result);
                }
                _store.TryRemove(requestId, out _);
            }
            return Task.FromResult<SpinResult?>(null);
        }

        public Task SaveResponseAsync(string requestId, SpinResult result, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            _store[requestId] = new CacheEntry
            {
                Result = result,
                ExpiresAt = DateTime.UtcNow.Add(ttl)
            };
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Cung cấp danh sách Dynamic Endpoints (GET api/index) như trong kiến trúc gốc của myKONAMI.
    /// </summary>
    public class DynamicEndpointsProvider
    {
        public static object GetEndpoints()
        {
            return new
            {
                status = "OK",
                endpoints = new
                {
                    auth = "/api/auth/login",
                    startup = "/api/startup",
                    spin = "/api/slot/spin",
                    wallet = "/api/wallet/balances",
                    dailyBonus = "/api/dailybonus_v2/collect",
                    hourlyBonus = "/api/hourlybonus/collect"
                },
                serverTimeUtc = DateTime.UtcNow
            };
        }
    }
}
