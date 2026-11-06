using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SpinEngine.Application.Commands;
using SpinEngine.Application.Interfaces;
using SpinEngine.Infrastructure.Services;
using SpinEngine.Shared.Contracts;

namespace SpinEngine.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IndexController : ControllerBase
    {
        /// <summary>
        /// Khám phá Endpoint động (Dynamic Discovery) tương đương GET api/index trong myKONAMI.
        /// </summary>
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(DynamicEndpointsProvider.GetEndpoints());
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class WalletController : ControllerBase
    {
        private readonly IWalletRepository _walletRepo;

        public WalletController(IWalletRepository walletRepo)
        {
            _walletRepo = walletRepo;
        }

        [HttpGet("balances")]
        public async Task<IActionResult> GetBalances([FromQuery] string playerId = "demo_player")
        {
            var wallet = await _walletRepo.GetWalletAsync(playerId);
            return Ok(wallet.ToCommonDto());
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class SlotController : ControllerBase
    {
        private readonly SpinCommandHandler _spinHandler;

        public SlotController(SpinCommandHandler spinHandler)
        {
            _spinHandler = spinHandler;
        }

        /// <summary>
        /// Thực hiện vòng quay slot (Spin).
        /// Server-authoritative: Kiểm tra số dư, trừ tiền, sinh RNG, đánh giá Paytable và trả kết quả.
        /// </summary>
        [HttpPost("spin")]
        public async Task<ActionResult<SpinResult>> Spin(
            [FromBody] SpinRequest request,
            [FromHeader(Name = "X-Request-Id")] string? requestId,
            [FromQuery] string playerId = "demo_player")
        {
            var command = new SpinCommand
            {
                PlayerId = playerId,
                RequestId = requestId ?? System.Guid.NewGuid().ToString("N"),
                Request = request
            };

            var result = await _spinHandler.HandleAsync(command, HttpContext.RequestAborted);
            return Ok(result);
        }
    }
}
