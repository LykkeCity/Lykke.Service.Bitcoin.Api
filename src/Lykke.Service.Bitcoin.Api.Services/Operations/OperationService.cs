using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lykke.Service.Bitcoin.Api.Core.Domain.Operation;
using Lykke.Service.Bitcoin.Api.Core.Domain.Transactions;
using Lykke.Service.Bitcoin.Api.Core.Services.Exceptions;
using Lykke.Service.Bitcoin.Api.Core.Services.Operation;
using Lykke.Service.Bitcoin.Api.Core.Services.TransactionOutputs;
using Lykke.Service.Bitcoin.Api.Core.Services.Transactions;
using NBitcoin;
using NBitcoin.JsonConverters;

namespace Lykke.Service.Bitcoin.Api.Services.Operations
{
    public class OperationService : IOperationService
    {
        private readonly Network _network;
        private readonly IOperationMetaRepository _operationMetaRepository;
        private readonly ITransactionBlobStorage _transactionBlobStorage;
        private readonly ITransactionBuilderService _transactionBuilder;
        private readonly ITransactionOutputsService _transactionOutputsService;

        public OperationService(ITransactionBuilderService transactionBuilder,
            IOperationMetaRepository operationMetaRepository,
            ITransactionOutputsService transactionOutputsService,
            ITransactionBlobStorage transactionBlobStorage, Network network)
        {
            _transactionBuilder = transactionBuilder;
            _operationMetaRepository = operationMetaRepository;
            _transactionOutputsService = transactionOutputsService;
            _transactionBlobStorage = transactionBlobStorage;
            _network = network;
        }

        public async Task<BuiltTransactionInfo> GetOrBuildTransferTransactionAsync(Guid operationId,
            IList<OperationInput> inputs,
            IList<OperationOutput> outputs,
            OperationType operationType,
            string assetId,
            bool includeFee)
        {
            var existingOperation = await _operationMetaRepository.GetAsync(operationId);
            if (existingOperation != null)
            {
                if (!OperationMetaComparer.Compare(existingOperation, inputs, outputs, assetId, includeFee))
                    throw new BusinessException("Conflict in operation parameters", ErrorCode.Conflict);
                return await GetExistingTransaction(existingOperation.OperationId, existingOperation.Hash);
            }
            IBuiltTransaction builtTransaction;
            switch (operationType)
            {
                case OperationType.Single:
                    builtTransaction = await _transactionBuilder.GetTransferTransactionAsync(inputs.Single(), outputs.Single(), includeFee);

                    break;
                case OperationType.ManyInputs:
                    builtTransaction = await _transactionBuilder.GetManyInputsTransferTransactionAsync(inputs, outputs.Single(), includeFee);

                    break;
                case OperationType.ManyOutputs:
                    builtTransaction = await _transactionBuilder.GetManyOutputsTransferTransactionAsync(inputs.Single(), outputs, includeFee);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operationType));
            }

            var builtTransactionInfo = new BuiltTransactionInfo
            {
                TransactionHex = builtTransaction.TransactionData.ToHex(),
                UsedCoins = builtTransaction.UsedCoins
            };

            await _transactionOutputsService.AddInternalOutputs(operationId,
                builtTransaction.TransactionData.Outputs.AsCoins());

            var txHash = builtTransaction.TransactionData.GetHash().ToString();

            await _transactionBlobStorage.AddOrReplaceTransactionAsync(operationId, txHash, TransactionBlobType.Initial,
                builtTransactionInfo.ToJson(_network));

            var operation = OperationMeta.Create(operationId, txHash, inputs, outputs, assetId, builtTransaction.Fee.Satoshi, includeFee);

            if (await _operationMetaRepository.TryInsertAsync(operation))
                return builtTransactionInfo;

            existingOperation = await _operationMetaRepository.GetAsync(operationId);
            return await GetExistingTransaction(operationId, existingOperation.Hash);
        }

        private async Task<BuiltTransactionInfo> GetExistingTransaction(Guid operationId, string hash)
        {
            var alreadyBuiltTransaction =
                await _transactionBlobStorage.GetTransactionAsync(operationId, hash, TransactionBlobType.Initial);
            return Serializer.ToObject<BuiltTransactionInfo>(alreadyBuiltTransaction);
        }
    }
}
