using System.Security.Cryptography;
using SpinEngine.Domain.Entities;

namespace SpinEngine.Domain.Services
{
    public interface ISlotRngService
    {
        /// <summary>
        /// Sinh mảng chỉ số dừng (Stop Indices) cho từng cuộn một cách ngẫu nhiên và bảo mật.
        /// </summary>
        int[] GenerateStopIndices(SlotConfiguration config);

        /// <summary>
        /// Từ các chỉ số dừng và dải cuộn, trích xuất ra ma trận biểu tượng hiển thị trên màn hình [Reels x Rows].
        /// </summary>
        string[][] BuildVisibleMatrix(SlotConfiguration config, int[] stopIndices);
    }

    /// <summary>
    /// Triển khai thuật toán RNG chuẩn công nghiệp casino.
    /// Dùng System.Security.Cryptography.RandomNumberGenerator để chống tấn công đoán trước seed.
    /// </summary>
    public class SlotRngService : ISlotRngService
    {
        public int[] GenerateStopIndices(SlotConfiguration config)
        {
            int reelCount = config.Dimensions.Reels;
            int[] stopIndices = new int[reelCount];

            for (int r = 0; r < reelCount; r++)
            {
                string reelKey = r.ToString();
                if (!config.ReelStrips.TryGetValue(reelKey, out var strip) || strip.Length == 0)
                {
                    stopIndices[r] = 0;
                    continue;
                }

                // RandomNumberGenerator.GetInt32 là hàm bảo mật ngẫu nhiên không thiên vị
                stopIndices[r] = RandomNumberGenerator.GetInt32(0, strip.Length);
            }

            return stopIndices;
        }

        public string[][] BuildVisibleMatrix(SlotConfiguration config, int[] stopIndices)
        {
            int reelCount = config.Dimensions.Reels;
            int rowCount = config.Dimensions.Rows;

            string[][] matrix = new string[reelCount][];

            for (int r = 0; r < reelCount; r++)
            {
                matrix[r] = new string[rowCount];
                string reelKey = r.ToString();
                var strip = config.ReelStrips[reelKey];
                int stripLength = strip.Length;
                int stop = stopIndices[r];

                for (int row = 0; row < rowCount; row++)
                {
                    // Lấy symbol xoay vòng (Wrap-around) trên dải cuộn
                    int symbolIndex = (stop + row) % stripLength;
                    matrix[r][row] = strip[symbolIndex];
                }
            }

            return matrix;
        }
    }
}
