using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lykke.Service.Bitcoin.Api.Core.Domain.Outputs;
using Lykke.Service.Bitcoin.Api.Core.Services;
using Lykke.Service.Bitcoin.Api.Core.Services.BlockChainReaders;
using Lykke.Service.Bitcoin.Api.Core.Services.TransactionOutputs;
using NBitcoin;

namespace Lykke.Service.Bitcoin.Api.Services.TransactionOutputs
{
    public class TransactionOutputsService : ITransactionOutputsService
    {
        private readonly IBlockChainProvider _blockChainProvider;
        private readonly IInternalOutputRepository _internalOutputRepository;
        private readonly IUnspentCoinsProvider _unspentCoinsProvider;
        private readonly Network _network;
        private readonly ISpentOutputRepository _spentOutputRepository;

        public TransactionOutputsService(IBlockChainProvider blockChainProvider,
            Network network,
            ISpentOutputRepository spentOutputRepository,
            IInternalOutputRepository internalOutputRepository,
            IUnspentCoinsProvider unspentCoinsProvider
            )
        {
            _blockChainProvider = blockChainProvider;
            _network = network;
            _spentOutputRepository = spentOutputRepository;
            _internalOutputRepository = internalOutputRepository;
            _unspentCoinsProvider = unspentCoinsProvider;
        }

        public async Task<IEnumerable<Coin>> GetUnspentOutputsAsync(string address, int confirmationsCount)
        {
            var blockchainOutputs = await _blockChainProvider.GetUnspentOutputsAsync(address, confirmationsCount);

            return await _unspentCoinsProvider.FilterAsync(await AddInternalOutputsAsync(blockchainOutputs, address, confirmationsCount));
        }

        public Task AddInternalOutputs(Guid operationId, IEnumerable<Coin> coins)
        {
            return _internalOutputRepository.InsertOrReplaceOutputsAsync(coins.Select(c => new InternalOutput
            {
                OperationId = operationId,
                Address = c.TxOut.ScriptPubKey.GetDestinationAddress(_network).ToString(),
                Amount = c.TxOut.Value.Satoshi,
                N = (int) c.Outpoint.N,
                ScriptPubKey = c.TxOut.ScriptPubKey.ToHex()
            }));
        }

        public async Task CompleteTxOutputs(Guid operationId, Transaction tx)
        {
            var inputs = tx.Inputs.Select(o => new Output(o.PrevOut)).ToList();
            await SetInternalOutputsTxHash(operationId, tx.GetHash().ToString());
            await _spentOutputRepository.InsertSpentOutputsAsync(operationId, inputs);
            await RemoveUsedInternalOutputs(inputs);
        }

        private async Task<IList<Coin>> AddInternalOutputsAsync(IList<Coin> blockchainOutputs, string address,
            int confirmationsCount)
        {
            if (confirmationsCount == 0)
            {
                var set = new HashSet<OutPoint>(blockchainOutputs.Select(x => x.Outpoint));
                var internalSavedOutputs = (await _internalOutputRepository.GetOutputsAsync(address))
                    .Where(o => !string.IsNullOrEmpty(o.TransactionHash))
                    .Where(o => !set.Contains(new OutPoint(uint256.Parse(o.TransactionHash), o.N)));

                return blockchainOutputs.Concat(internalSavedOutputs.Select(o =>
                {
                    var coin = new Coin(new OutPoint(uint256.Parse(o.TransactionHash), o.N),
                        new TxOut(new Money(o.Amount, MoneyUnit.Satoshi), o.ScriptPubKey.ToScript()));
                    return coin;
                })).ToList();
            }

            return blockchainOutputs;
        }

        public Task SetInternalOutputsTxHash(Guid operationId, string txHash)
        {
            return _internalOutputRepository.SetTxHashAsync(operationId, txHash);
        }

        public Task RemoveUsedInternalOutputs(IEnumerable<IOutput> outputs)
        {
            return _internalOutputRepository.RemoveOutputsAsync(outputs);
        }
    }
}
