using System;

namespace SpinEngine.Shared.Contracts
{
    /// <summary>
    /// Gói dữ liệu yêu cầu quay slot từ Client gửi lên Server.
    /// Kiến trúc Server-Authoritative: Client KHÔNG tự tính kết quả, chỉ gửi ý định cược.
    /// </summary>
    public class SpinRequest
    {
        /// <summary>
        /// Mã định danh máy slot người chơi đang tham gia (ví dụ: "classic_777", "china_shores").
        /// </summary>
        public string GameId { get; set; } = string.Empty;

        /// <summary>
        /// Tổng số tiền xu (Credits) người chơi đặt cược cho ván này.
        /// Backend sẽ kiểm tra số dư ví xem có đủ tiền không trước khi trừ tiền.
        /// </summary>
        public long FullWager { get; set; }

        /// <summary>
        /// Số đường trả thưởng (Paylines) đang kích hoạt (Client Paylines - CLP).
        /// </summary>
        public int ClientPaylines { get; set; } = 20;

        /// <summary>
        /// Free Spin Remaining Token (FSRT):
        /// Tại sao cần cái này? Khi người chơi trúng lượt quay miễn phí (Free Spins),
        /// Server sẽ cấp một Token phiên. Những lượt quay tiếp theo Client gửi Token này lên,
        /// Server xác thực hợp lệ thì sẽ CHO QUAY MIỄN PHÍ (cược 0 đồng).
        /// Nếu không có token này do Server cấp, hacker có thể tự sửa client để quay miễn phí mãi mãi.
        /// </summary>
        public string? FreeSpinRemainingToken { get; set; }

        /// <summary>
        /// Cấu hình tuỳ chọn đặc biệt của ván chơi (Strategy Options - SO).
        /// </summary>
        public string? StrategyOptions { get; set; }
    }
}
