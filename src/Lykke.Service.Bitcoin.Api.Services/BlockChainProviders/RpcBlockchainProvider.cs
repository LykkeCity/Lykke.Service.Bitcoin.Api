using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AsyncFriendlyStackTrace;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Service.Bitcoin.Api.Core.Services.Address;
using Lykke.Service.Bitcoin.Api.Core.Services.BlockChainReaders;
using Lykke.Service.Bitcoin.Api.Core.Services.Exceptions;
using NBitcoin;
using NBitcoin.RPC;

namespace Lykke.Service.Bitcoin.Api.Services.BlockChainProviders
{
    public class RpcBlockchainProvider : IBlockChainProvider
    {
        private readonly RPCClient _client;
        private readonly ILog _log;
        private readonly IAddressValidator _addressValidator;

        public RpcBlockchainProvider(RPCClient client, 
            ILogFactory logFactory, IAddressValidator addressValidator)
        {
            _client = client;
            _addressValidator = addressValidator;
            _log = logFactory.CreateLog(nameof(RpcBlockchainProvider));
        }

        public async Task BroadCastTransactionAsync(Transaction tx)
        {
            _log.Info("Using RPC Provider to Broadcast");
            try
            {
                await _client.SendRawTransactionAsync(tx);
            }
            catch (RPCException ex) when (ex.RPCCode == RPCErrorCode.RPC_VERIFY_REJECTED && ex.Message == "257: txn-already-known")
            {
                throw new BusinessException("Transaction already brodcasted", ErrorCode.TransactionAlreadyBroadcasted);
            }
        }

        public async Task<int> GetTxConfirmationCountAsync(string txHash)
        {
            _log.Info("Using RPC Provider to GetTxConfirmationCountAsync");
            try
            {
                var tx = await _client.GetRawTransactionInfoAsync(uint256.Parse(txHash));

                return (int)tx.Confirmations;
            }
            catch (RPCException e) when(e.RPCCode == RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY)
            {
                _log.Info(nameof(RpcBlockchainProvider), nameof(GetTxConfirmationCountAsync), $"Tx not found {e.ToAsyncString()}");

                return 0;
            }
        }

        public async Task<IList<Coin>> GetUnspentOutputsAsync(string address, int minConfirmationCount)
        {
            _log.Info("Using RPC Provider to GetUnspentOutputsAsync");
            var btcAddr = _addressValidator.GetBitcoinAddress(address);

            if (btcAddr == null)
            {
                throw new ArgumentException("Unable to recognize address", nameof(address));
            }

            var rpcResponse = await _client.ListUnspentAsync(minConfirmationCount, int.MaxValue, btcAddr);

            return rpcResponse.Select(p => new Coin(p.OutPoint, new TxOut(p.Amount, p.ScriptPubKey))).ToList();
        }

        public Task<IList<ColoredCoin>> GetColoredUnspentOutputsAsync(string address, int minConfirmationCount)
        {
            throw new System.NotImplementedException();
        }

        public Task<int> GetLastBlockHeightAsync()
        {
            _log.Info("Using RPC Provider to GetLastBlockHeightAsync");
            return _client.GetBlockCountAsync();
        }

        public Task<IEnumerable<BitcoinTransaction>> GetTransactionsAfterTxAsync(string address, string afterHash)
        {
            throw new System.NotImplementedException();
        }

        public Task<IEnumerable<string>> GetInvolvedInTxAddresses(string txHash)
        {
            throw new System.NotImplementedException();
        }

        public Task<IEnumerable<(string txHash, IEnumerable<string> destinationAddresses)>> GetTxOutputAddresses(int blockHeight)
        {
            throw new System.NotImplementedException();
        }
    }
}
