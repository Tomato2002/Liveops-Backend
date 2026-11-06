using System;

namespace SpinEngine.Shared.Contracts
{
    /// <summary>
    /// Chứa thông tin 4 loại số dư tài chính của người chơi.
    /// Toàn bộ dùng kiểu `long` để tránh tràn số và triệt tiêu sai số làm tròn.
    /// </summary>
    public class BalancesDto
    {
        /// <summary>
        /// Tiền xu ảo dùng để đặt cược và trả thưởng khi quay slot.
        /// </summary>
        public long Credits { get; set; }

        /// <summary>
        /// Điểm kinh nghiệm tích lũy để tăng Level người chơi.
        /// </summary>
        public long Experience { get; set; }

        /// <summary>
        /// Điểm khách hàng thân thiết (VIP Points / Loyalty) dùng để đổi quà ngoài đời thực.
        /// </summary>
        public long Loyalty { get; set; }

        /// <summary>
        /// Tiền tệ cao cấp (Gems / Kim cương) kiếm được qua nạp thẻ hoặc sự kiện đặc biệt.
        /// </summary>
        public long Premium { get; set; }
    }

    /// <summary>
    /// Common DTO: Gói dữ liệu dùng chung luôn được Server tự động gắn vào
    /// mọi response nghiệp vụ để Client cập nhật đồng bộ UI.
    /// </summary>
    public class CommonDto
    {
        /// <summary>
        /// Số dư 4 ví mới nhất sau khi thực thi giao dịch.
        /// </summary>
        public BalancesDto Balances { get; set; } = new BalancesDto();

        /// <summary>
        /// Cấp độ hiện tại của người chơi.
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Điểm kinh nghiệm tích lũy trong cấp độ hiện tại (dùng để vẽ thanh tiến trình %).
        /// </summary>
        public long LevelIndexPoint { get; set; }

        /// <summary>
        /// Thời gian chuẩn UTC tại Server (giúp Client đồng bộ đồng hồ đếm ngược sự kiện).
        /// </summary>
        public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
    }
}
