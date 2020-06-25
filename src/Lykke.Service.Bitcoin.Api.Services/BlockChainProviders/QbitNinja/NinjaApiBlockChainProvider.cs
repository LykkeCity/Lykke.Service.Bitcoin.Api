using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Lykke.Service.Bitcoin.Api.Core.Services.Address;
using Lykke.Service.Bitcoin.Api.Core.Services.BlockChainReaders;
using Lykke.Service.Bitcoin.Api.Services.Wallet;
using NBitcoin;
using NBitcoin.RPC;
using QBitNinja.Client;
using QBitNinja.Client.Models;

namespace Lykke.Service.Bitcoin.Api.Services.BlockChainProviders.QbitNinja
{
    public class NinjaApiBlockChainProvider : IBlockChainProvider
    {
        private readonly IAddressValidator _addressValidator;
        private readonly Network _network;
        private readonly QBitNinjaClient _ninjaClient;
        private readonly RPCClient _rpcClient;
        private readonly BlockHeightSettings _blockHeightSettings;

        public NinjaApiBlockChainProvider(QBitNinjaClient ninjaClient, RPCClient rpcClient, Network network,
            IAddressValidator addressValidator, BlockHeightSettings blockHeightSettings)
        {
            _ninjaClient = ninjaClient;
            _rpcClient = rpcClient;
            _ninjaClient.Colored = true;
            _network = network;
            _addressValidator = addressValidator;
            _blockHeightSettings = blockHeightSettings;
        }

        public Task BroadCastTransactionAsync(Transaction tx)
        {
            return _rpcClient.SendRawTransactionAsync(tx);
        }

        public async Task<int> GetTxConfirmationCountAsync(string txHash)
        {
            var tx = await _ninjaClient.GetTransaction(uint256.Parse(txHash));
            return tx?.Block?.Confirmations ?? 0;
        }

        public async Task<IList<Coin>> GetUnspentOutputsAsync(string address, int minConfirmationCount)
        {
            return (await GetAllUnspentOutputs(address, minConfirmationCount)).OfType<Coin>().ToList();
        }

        public async Task<IList<ColoredCoin>> GetColoredUnspentOutputsAsync(string address, int minConfirmationCount)
        {
            return (await GetAllUnspentOutputs(address, minConfirmationCount)).OfType<ColoredCoin>().ToList();
        }

        public async Task<int> GetLastBlockHeightAsync()
        {
            var block = await _ninjaClient.GetBlock(BlockFeature.Parse("tip"), true);
            return block.AdditionalInformation.Height;
        }

        public async Task<IEnumerable<BitcoinTransaction>> GetTransactionsAfterTxAsync(string address, string afterHash)
        {
            var heightTo = 0;
            if (!string.IsNullOrEmpty(afterHash))
            {
                var tx = await _ninjaClient.GetTransaction(uint256.Parse(afterHash));
                heightTo = tx?.Block?.Height ?? 0;
            }

            var operations = await _ninjaClient.GetBalanceBetween(new BalanceSelector(address),
                BlockFeature.Parse("tip"), new BlockFeature(heightTo));

            return (await operations.Operations.SelectAsync(async op =>
            {
                var tx = await _ninjaClient.GetTransaction(op.TransactionId);
                return new BitcoinTransaction
                {
                    Hash = op.TransactionId.ToString(),
                    Timestamp = tx.FirstSeen.DateTime,
                    Inputs = tx.SpentCoins.Select(i => new BitcoinInput
                    {
                        Address = i.TxOut.ScriptPubKey.GetDestinationAddress(_network)?.ToString(),
                        Value = i.TxOut.Value
                    }).ToList(),
                    Outputs = tx.ReceivedCoins.Select(output => new BitcoinOutput
                    {
                        Address = output.TxOut.ScriptPubKey.GetDestinationAddress(_network)?.ToString(),
                        Value = output.TxOut.Value
                    }).ToList()
                };
            })).OrderBy(o => o.Timestamp).ToList();
        }

        public async Task<IEnumerable<string>> GetInvolvedInTxAddresses(string txHash)
        {
            var fullTxData = await _ninjaClient.GetTransaction(uint256.Parse(txHash));

            var inputAddresses = fullTxData.SpentCoins.Select(p => p.TxOut.ScriptPubKey.GetDestinationAddress(_network));
            var outputAddresses = fullTxData.ReceivedCoins.Select(p => p.TxOut.ScriptPubKey.GetDestinationAddress(_network));

            return inputAddresses
                .Union(outputAddresses)
                .Where(addr => addr != null) // colored address marker
                .Select(p => p.ToString())
                .ToList();
        }

        public async Task<IEnumerable<(string txHash, IEnumerable<string> destinationAddresses)>> GetTxOutputAddresses(int blockHeight)
        {
            var blockResponse =  await _ninjaClient.GetBlock(BlockFeature.Parse(blockHeight.ToString()));
            if (blockResponse == null)
            {
                throw new ArgumentException("Block not found", nameof(blockHeight));
            }

            var result = new List<(string txHash, IEnumerable<string> destinationAddresses)>();

            foreach (var tx in blockResponse.Block.Transactions)
            {
                var destinationAddresses = tx.Outputs.AsIndexedOutputs().Select(p => p.TxOut.ScriptPubKey
                    .GetDestinationAddress(_network)
                    ?.ToString())
                    .Where(p => p != null);

                result.Add((txHash: tx.GetHash().ToString(), destinationAddresses: destinationAddresses));
            }

            return result;
        }

        private async Task<IList<ICoin>> GetAllUnspentOutputs(string address, int minConfirmationCount)
        {
            var response = await _ninjaClient.GetBalance(_addressValidator.GetBitcoinAddress(address), true);
            return response.Operations
                .Where(o => o.Height >= _blockHeightSettings.IgnoreUnspentOutputsBeforeBlockHeight)
                .Where(o => o.Confirmations >= minConfirmationCount)
                .SelectMany(o => o.ReceivedCoins).ToList();
        }
    }
}
