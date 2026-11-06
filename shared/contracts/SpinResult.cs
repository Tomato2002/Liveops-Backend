using System;
using System.Collections.Generic;

namespace SpinEngine.Shared.Contracts
{
    /// <summary>
    /// Toạ độ vị trí của biểu tượng trên màn hình ma trận cuộn (Reel Index, Row Index).
    /// Giúp Client biết chính xác biểu tượng nào chiến thắng để phát hiệu ứng hoạt họa viền sáng.
    /// </summary>
    public class SymbolCoordinate
    {
        public int Reel { get; set; } // Cột (0 đến 4 cho máy 5 cột)
        public int Row { get; set; }  // Hàng (0 đến 2 cho máy 3 hàng)

        public SymbolCoordinate() { }

        public SymbolCoordinate(int reel, int row)
        {
            Reel = reel;
            Row = row;
        }
    }

    /// <summary>
    /// Thông tin chi tiết của một đường trả thưởng chiến thắng (Payline Win).
    /// </summary>
    public class PaylineWinInfo
    {
        /// <summary>
        /// Chỉ số đường cược thắng (ví dụ: đường số 0, số 1...).
        /// </summary>
        public int PaylineIndex { get; set; }

        /// <summary>
        /// Mã định danh của biểu tượng tạo nên chiến thắng (ví dụ: "SEVEN", "BAR", "CHERRY").
        /// </summary>
        public string SymbolId { get; set; } = string.Empty;

        /// <summary>
        /// Số lượng biểu tượng liên tiếp khớp nhau (3, 4 hoặc 5 biểu tượng).
        /// </summary>
        public int MatchCount { get; set; }

        /// <summary>
        /// Hệ số nhân thưởng tra cứu từ bảng trả thưởng (Paytable Multiplier).
        /// </summary>
        public int WinMultiplier { get; set; }

        /// <summary>
        /// Số tiền xu thắng thực tế trên đường này (Tiền cược mỗi dòng * Hệ số nhân).
        /// </summary>
        public long WinAmount { get; set; }

        /// <summary>
        /// Danh sách toạ độ các biểu tượng thắng cuộc trên ma trận để Client vẽ viền sáng.
        /// </summary>
        public List<SymbolCoordinate> WinningPositions { get; set; } = new List<SymbolCoordinate>();
    }

    /// <summary>
    /// Gói dữ liệu kết quả vòng quay Server gửi về cho Client trình diễn.
    /// Toàn bộ tính toán ngẫu nhiên (RNG) và trả thưởng đều do Server quyết định.
    /// </summary>
    public class SpinResult
    {
        /// <summary>
        /// Mã định danh duy nhất của ván quay (GUID) dùng để tra cứu lịch sử và kiểm toán.
        /// </summary>
        public string RoundId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Vị trí điểm dừng của từng cuộn trên dải cuộn (Reel Strip Stop Indices).
        /// Ví dụ máy 5 cuộn: [12, 45, 8, 93, 22].
        /// </summary>
        public int[] StopIndices { get; set; } = Array.Empty<int>();

        /// <summary>
        /// Ma trận biểu tượng hiển thị trên màn hình: Mảng [5 cột][3 hàng].
        /// Client sẽ đọc ma trận này để hiển thị đúng các symbol sau khi các cuộn dừng lại.
        /// </summary>
        public string[][] VisibleMatrix { get; set; } = Array.Empty<string[]>();

        /// <summary>
        /// Tổng số tiền xu người chơi thắng được trong ván quay này (Tổng các đường thắng).
        /// Nếu không trúng đường nào, giá trị là 0.
        /// </summary>
        public long TotalWin { get; set; }

        /// <summary>
        /// Danh sách các đường thắng chi tiết.
        /// </summary>
        public List<PaylineWinInfo> PaylineWins { get; set; } = new List<PaylineWinInfo>();

        /// <summary>
        /// Ván này có kích hoạt tính năng Free Spin (quay miễn phí) không?
        /// </summary>
        public bool IsFreeSpinTriggered { get; set; }

        /// <summary>
        /// Số lượt quay miễn phí được thưởng thêm (nếu có).
        /// </summary>
        public int FreeSpinsGranted { get; set; }

        /// <summary>
        /// Số lượt Free Spin còn lại chưa sử dụng.
        /// </summary>
        public int RemainingFreeSpins { get; set; }

        /// <summary>
        /// Token bảo vệ cho các ván Free Spin tiếp theo (Server cấp).
        /// </summary>
        public string? NextFreeSpinToken { get; set; }

        /// <summary>
        /// Gói đồng bộ số dư tự động: Mang theo số dư tiền, kinh nghiệm, level mới nhất.
        /// </summary>
        public CommonDto CommonDto { get; set; } = new CommonDto();
    }
}
