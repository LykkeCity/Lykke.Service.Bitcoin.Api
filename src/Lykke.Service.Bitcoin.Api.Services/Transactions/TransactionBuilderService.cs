using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lykke.Service.Bitcoin.Api.Core.Domain.Operation;
using Lykke.Service.Bitcoin.Api.Core.Services.Address;
using Lykke.Service.Bitcoin.Api.Core.Services.Exceptions;
using Lykke.Service.Bitcoin.Api.Core.Services.Fee;
using Lykke.Service.Bitcoin.Api.Core.Services.Operation;
using Lykke.Service.Bitcoin.Api.Core.Services.TransactionOutputs;
using Lykke.Service.Bitcoin.Api.Core.Services.Transactions;
using Lykke.Service.Bitcoin.Api.Services.Operations;
using NBitcoin;

namespace Lykke.Service.Bitcoin.Api.Services.Transactions
{
    public class TransactionBuilderService : ITransactionBuilderService
    {
        private readonly IFeeService _feeService;
        private readonly ITransactionOutputsService _transactionOutputsService;
        private readonly IAddressValidator _addressValidator;
        private readonly OperationsConfirmationsSettings _confirmationsSettings;

        public TransactionBuilderService(ITransactionOutputsService transactionOutputsService,
            IAddressValidator addressValidator,
            IFeeService feeService,
            OperationsConfirmationsSettings confirmationsSettings)
        {
            _transactionOutputsService = transactionOutputsService;
            _addressValidator = addressValidator;
            _feeService = feeService;
            _confirmationsSettings = confirmationsSettings;
        }

        private async Task<IList<Coin>> GetUnspentCoins(OperationBitcoinInput input, int minConfirmations)
        {
            var coins = (await _transactionOutputsService.GetUnspentOutputsAsync(input.Address.ToString(), minConfirmations)).ToList();
            if (input.Redeem != null)
                coins = coins.Select(o => o.ToScriptCoin(input.Redeem)).Cast<Coin>().ToList();
            return coins;
        }


        public async Task<IBuiltTransaction> GetManyOutputsTransferTransactionAsync(OperationInput fromAddress,
            IList<OperationOutput> toAddresses,
            bool includeFee)
        {
            var input = fromAddress.ToBitcoinInput(_addressValidator);
            var outputs = toAddresses.Select(o => o.ToBitcoinOutput(_addressValidator)).ToList();
            var builder = new TransactionBuilder();

            foreach (var operationBitcoinOutput in outputs)
                builder.Send(operationBitcoinOutput.Address, operationBitcoinOutput.Amount);

            var minConfirmations = includeFee ? _confirmationsSettings.MinConfirmationsToDetectOperation : 0;
            var coins = await GetUnspentCoins(input, minConfirmations);

            var addressBalance = coins.Sum(o => o.Amount);
            var sendAmount = outputs.Sum(o => o.Amount); ;
            var sentFees = Money.Zero;

            builder.SetChange(input.Address);

            var change = addressBalance - sendAmount;      
            if (change < new TxOut(Money.Zero, input.Address).GetDustThreshold(builder.StandardTransactionPolicy.MinRelayTxFee) && change > 0)
            {
                builder.SendFees(change);
                sentFees = change;
            }                            

            builder.AddCoins(coins);

            var calculatedFee = await _feeService.CalcFeeForTransactionAsync(builder) - sentFees;

            var requiredBalance = sendAmount + calculatedFee;           

            if (addressBalance < requiredBalance)
                throw new BusinessException($"The sum of total applicable outputs is less than the required : {requiredBalance} satoshis.", ErrorCode.NotEnoughFundsAvailable);

            if (calculatedFee > 0)
                builder.SendFees(calculatedFee);

            var tx = builder.BuildTransaction(false);
            var usedCoins = builder.FindSpentCoins(tx).ToArray();

            return BuiltTransaction.Create(tx, tx.GetFee(usedCoins), usedCoins.Cast<Coin>());
        }



        public async Task<IBuiltTransaction> GetManyInputsTransferTransactionAsync(IList<OperationInput> fromAddresses, 
            OperationOutput toAddress,
            bool includeFee)
        {
            var inputs = fromAddresses.Select(o => o.ToBitcoinInput(_addressValidator)).ToList();
            var output = toAddress.ToBitcoinOutput(_addressValidator);

            var builder = new TransactionBuilder();

            var totalBalance = Money.Zero;
            var sendAmount = inputs.Sum(o => o.Amount);
            var sentFees = Money.Zero;
            var minConfirmations = includeFee ? _confirmationsSettings.MinConfirmationsToDetectOperation : 0;

            foreach (var operationBitcoinInput in inputs)
            {
                var coins = await GetUnspentCoins(operationBitcoinInput, minConfirmations);
                builder.AddCoins(coins);

                var addressBalance = coins.Sum(o => o.Amount);

                if (addressBalance < operationBitcoinInput.Amount)
                    throw new BusinessException($"The sum of total applicable outputs is less than the required : {operationBitcoinInput.Amount.Satoshi} satoshis.",
                        ErrorCode.NotEnoughFundsAvailable);

                // send change to source address
                var change = addressBalance - operationBitcoinInput.Amount;
                if (change < new TxOut(Money.Zero, operationBitcoinInput.Address).GetDustThreshold(builder.StandardTransactionPolicy.MinRelayTxFee))
                {
                    builder.SendFees(change);
                    sentFees += change;
                }
                else
                    builder.Send(operationBitcoinInput.Address, change);
                totalBalance += addressBalance;
            }
            builder.Send(output.Address, sendAmount);

            var calculatedFee = await _feeService.CalcFeeForTransactionAsync(builder) - sentFees;

            var requiredBalance = sendAmount;

            if (totalBalance < requiredBalance)
                throw new BusinessException(
                    $"The sum of total applicable outputs is less than the required : {requiredBalance} satoshis.",
                    ErrorCode.NotEnoughFundsAvailable);

            if (calculatedFee > sendAmount)
                throw new BusinessException(
                    $"The sum of total applicable outputs is less than the required fee:{calculatedFee} satoshis.",
                    ErrorCode.BalanceIsLessThanFee);
            if (calculatedFee > 0)
            {
                builder.SubtractFees();
                builder.SendFees(calculatedFee);
            }

            var tx = builder.BuildTransaction(false);

            var usedCoins = builder.FindSpentCoins(tx).ToArray();
            return BuiltTransaction.Create(tx, tx.GetFee(usedCoins), usedCoins.Cast<Coin>());
        }


        public async Task<IBuiltTransaction> GetTransferTransactionAsync(OperationInput fromAddress, OperationOutput toAddress, bool includeFee)
        {
            var input = fromAddress.ToBitcoinInput(_addressValidator);
            var output = toAddress.ToBitcoinOutput(_addressValidator);

            var builder = new TransactionBuilder();
            var minConfirmations = includeFee ? _confirmationsSettings.MinConfirmationsToDetectOperation : 0;
            var coins = await GetUnspentCoins(input, minConfirmations);
            builder.AddCoins(coins);

            var addressBalance = coins.Sum(o => o.Amount);

            if (addressBalance < input.Amount)
                throw new BusinessException($"The sum of total applicable outputs is less than the required : {input.Amount.Satoshi} satoshis.",
                    ErrorCode.NotEnoughFundsAvailable);

            var sentFees = Money.Zero;

            var change = addressBalance - input.Amount;
            if (change < new TxOut(Money.Zero, input.Address).GetDustThreshold(builder.StandardTransactionPolicy.MinRelayTxFee).Satoshi)
            {
                builder.SendFees(change);
                sentFees = change;
            }

            builder.Send(output.Address, input.Amount)
                   .SetChange(input.Address);

            var calculatedFee = await _feeService.CalcFeeForTransactionAsync(builder) - sentFees;

            var requiredBalance = input.Amount + (includeFee ? Money.Zero : calculatedFee);

            if (addressBalance < requiredBalance)
                throw new BusinessException(
                    $"The sum of total applicable outputs is less than the required : {requiredBalance} satoshis.",
                    ErrorCode.NotEnoughFundsAvailable);

            if (includeFee)
            {
                if (calculatedFee > input.Amount)
                    throw new BusinessException(
                        $"The sum of total applicable outputs is less than the required fee:{calculatedFee} satoshis.",
                        ErrorCode.BalanceIsLessThanFee);
                builder.SubtractFees();
            }

            if (calculatedFee > 0)
                builder.SendFees(calculatedFee);
            var tx = builder.BuildTransaction(false);

            var usedCoins = builder.FindSpentCoins(tx).ToArray();

            return BuiltTransaction.Create(tx, tx.GetFee(usedCoins), usedCoins.Cast<Coin>());
        }
    }
}
