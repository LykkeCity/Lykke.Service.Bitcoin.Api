using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using QBitNinja.Client.Models;

namespace Lykke.Service.Bitcoin.Api.Core.Services.BlockChainReaders
{
    public interface IBlockChainProvider
    {
        Task BroadCastTransactionAsync(Transaction tx);
        Task<int> GetTxConfirmationCountAsync(string txHash);
        Task<IList<Coin>> GetUnspentOutputsAsync(string address, int minConfirmationCount);
        Task<IList<ColoredCoin>> GetColoredUnspentOutputsAsync(string address, int minConfirmationCount);
        Task<int> GetLastBlockHeightAsync();
        Task<IEnumerable<BitcoinTransaction>> GetTransactionsAfterTxAsync(string address, string afterHash);
        Task<IEnumerable<string>> GetInvolvedInTxAddresses(string txHash);

        Task<IEnumerable<(string txHash, IEnumerable<string> destinationAddresses)>> GetTxOutputAddresses(
            int blockHeight);
    }
}
