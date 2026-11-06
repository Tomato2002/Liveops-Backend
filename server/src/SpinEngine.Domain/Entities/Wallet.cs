using System;
using SpinEngine.Shared.Contracts;

namespace SpinEngine.Domain.Entities
{
    public class InsufficientFundsException : Exception
    {
        public InsufficientFundsException(long currentBalance, long requestedWager)
            : base($"Số dư không đủ: Hiện có {currentBalance}, yêu cầu cược {requestedWager}.")
        {
        }
    }

    /// <summary>
    /// Thực thể Ví tiền của người chơi trong tầng Domain.
    /// Chứa toàn bộ quy tắc nghiệp vụ tài chính và thăng cấp.
    /// Tuyệt đối không phụ thuộc vào Database hay Web API.
    /// </summary>
    public class Wallet
    {
        public string PlayerId { get; set; } = string.Empty;
        public long Credits { get; private set; }
        public long Experience { get; private set; }
        public long Loyalty { get; private set; }
        public long Premium { get; private set; }
        public int Level { get; private set; } = 1;
        public long LevelIndexPoint { get; private set; }

        public Wallet() { }

        public Wallet(string playerId, long initialCredits = 1000000, long initialExp = 0, long initialLoyalty = 100, long initialPremium = 10)
        {
            PlayerId = playerId;
            Credits = initialCredits;
            Experience = initialExp;
            Loyalty = initialLoyalty;
            Premium = initialPremium;
            RecalculateLevel();
        }

        /// <summary>
        /// Khởi tạo lại trạng thái Wallet từ database.
        /// </summary>
        public static Wallet Restore(string playerId, long credits, long exp, long loyalty, long premium, int level, long levelIndexPoint)
        {
            return new Wallet
            {
                PlayerId = playerId,
                Credits = credits,
                Experience = exp,
                Loyalty = loyalty,
                Premium = premium,
                Level = level,
                LevelIndexPoint = levelIndexPoint
            };
        }

        /// <summary>
        /// Trừ tiền đặt cược: Kiểm tra nghiêm ngặt số dư trước khi trừ.
        /// </summary>
        public void DeductCredits(long wagerAmount)
        {
            if (wagerAmount < 0)
                throw new ArgumentException("Số tiền cược không thể âm.", nameof(wagerAmount));

            if (Credits < wagerAmount)
                throw new InsufficientFundsException(Credits, wagerAmount);

            Credits -= wagerAmount;

            // Mỗi lần cược được tích lũy điểm kinh nghiệm và điểm trung thành
            AddExperience(wagerAmount / 10);
            AddLoyalty(Math.Max(1, wagerAmount / 1000));
        }

        /// <summary>
        /// Cộng tiền thắng cược.
        /// </summary>
        public void AddCredits(long winAmount)
        {
            if (winAmount < 0)
                throw new ArgumentException("Tiền thắng không thể âm.", nameof(winAmount));

            Credits += winAmount;
        }

        public void AddExperience(long expPoints)
        {
            if (expPoints <= 0) return;
            Experience += expPoints;
            RecalculateLevel();
        }

        public void AddLoyalty(long loyaltyPoints)
        {
            if (loyaltyPoints <= 0) return;
            Loyalty += loyaltyPoints;
        }

        /// <summary>
        /// Công thức thăng cấp: Mỗi level cần (Level * 10,000) EXP.
        /// </summary>
        private void RecalculateLevel()
        {
            long currentExp = Experience;
            int level = 1;

            while (true)
            {
                long expRequiredForNextLevel = level * 10000L;
                if (currentExp >= expRequiredForNextLevel)
                {
                    currentExp -= expRequiredForNextLevel;
                    level++;
                }
                else
                {
                    break;
                }
            }

            Level = level;
            LevelIndexPoint = currentExp;
        }

        /// <summary>
        /// Xuất sang DTO để gửi cho Client.
        /// </summary>
        public CommonDto ToCommonDto()
        {
            return new CommonDto
            {
                Balances = new BalancesDto
                {
                    Credits = Credits,
                    Experience = Experience,
                    Loyalty = Loyalty,
                    Premium = Premium
                },
                Level = Level,
                LevelIndexPoint = LevelIndexPoint,
                ServerTimeUtc = DateTime.UtcNow
            };
        }
    }
}
