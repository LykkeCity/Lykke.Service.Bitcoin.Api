using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Common.Api.Contract.Responses;
using Lykke.Common.ApiLibrary.Exceptions;
using Lykke.Common.Log;
using Lykke.Service.Bitcoin.Api.Core.Services;
using Lykke.Service.Bitcoin.Api.Core.Services.Address;
using Lykke.Service.Bitcoin.Api.Core.Services.Exceptions;
using Lykke.Service.Bitcoin.Api.Services.Operations;
using Lykke.Service.BlockchainApi.Contract;
using Lykke.Service.BlockchainApi.Contract.Balances;
using Microsoft.AspNetCore.Mvc;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Lykke.Service.Bitcoin.Api.Controllers
{
    public class BalancesController : Controller
    {
        private readonly IAddressValidator _addressValidator;
        private readonly IWalletBalanceService _balanceService;
        private readonly OperationsConfirmationsSettings _confirmationsSettings;
        private readonly ILog _log;

        public BalancesController(IAddressValidator addressValidator, IWalletBalanceService balanceService, OperationsConfirmationsSettings confirmationsSettings,
            ILogFactory logFactory)
        {
            _addressValidator = addressValidator;
            _balanceService = balanceService;
            _confirmationsSettings = confirmationsSettings;
            _log = logFactory.CreateLog(this);
        }

        [HttpPost("api/balances/{address}/observation")]
        [SwaggerOperation(nameof(Subscribe))]
        [ProducesResponseType((int) HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesResponseType(typeof(ErrorResponse), 409)]
        public async Task<IActionResult> Subscribe(string address)
        {
            if (!_addressValidator.IsValid(address))
                throw new ValidationApiException($"Invalid address");

            try
            {
                await _balanceService.SubscribeAsync(address);
            }
            catch (BusinessException e) when (e.Code == ErrorCode.EntityAlreadyExist)
            {
                return StatusCode(409);
            }

            return Ok();
        }

        [HttpDelete("api/balances/{address}/observation")]
        [SwaggerOperation(nameof(Unsubscribe))]
        [ProducesResponseType((int) HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesResponseType(typeof(ErrorResponse), 204)]
        public async Task<IActionResult> Unsubscribe(string address)
        {
            if (!_addressValidator.IsValid(address))
                throw new ValidationApiException($"Invalid address");

            try
            {
                await _balanceService.UnsubscribeAsync(address);
            }
            catch (BusinessException e) when (e.Code == ErrorCode.EntityNotExist)
            {
                return StatusCode((int) HttpStatusCode.NoContent);
            }

            return Ok();
        }

        [HttpGet("api/balances/")]
        [SwaggerOperation(nameof(GetBalances))]
        [ProducesResponseType(typeof(PaginationResponse<WalletBalanceContract>), (int) HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        public async Task<IActionResult> GetBalances([FromQuery] int take, [FromQuery] string continuation)
        {
            if (take < 1)
                throw new ValidationApiException($"{nameof(take)} must be positive non zero integer");

            if (!string.IsNullOrEmpty(continuation))
                try
                {
                    JsonConvert.DeserializeObject<TableContinuationToken>(Utils.HexToString(continuation));
                }
                catch (JsonReaderException)
                {
                    throw new ValidationApiException($"{nameof(continuation)} must be valid continuation token");
                }

            var padedResult = await _balanceService.GetBalancesAsync(take, continuation);

            return Ok(PaginationResponse.From(padedResult.Continuation, padedResult.Items.Select(p =>
                new WalletBalanceContract
                {
                    Address = p.Address,
                    Balance = p.Balance.ToString(CultureInfo.InvariantCulture),
                    AssetId = p.AssetId,
                    Block = p.UpdatedAtBlockHeight
                }).ToList().AsReadOnly()));
        }

        [HttpPost("api/balances/force-update/{address}")]
        [SwaggerOperation(nameof(ForceUpdateBalance))]
        public async Task<IActionResult> ForceUpdateBalance(string address)
        {
            _log.Warning("Force updating the balance", context: new
            {
                Address =address,
                MinConfirmationsToDetectOperation = _confirmationsSettings.MinConfirmationsToDetectOperation
            });

            await _balanceService.UpdateBalanceAsync(address, _confirmationsSettings.MinConfirmationsToDetectOperation);

            return Ok();
        }
    }
}
