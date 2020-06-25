using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lykke.Service.Bitcoin.Api.Core.Domain.Outputs;
using Lykke.Service.Bitcoin.Api.Core.Services;
using Lykke.Service.Bitcoin.Api.Core.Services.TransactionOutputs;
using NBitcoin;

namespace Lykke.Service.Bitcoin.Api.Services.Wallet
{
    public class UnspentCoinsProvider : IUnspentCoinsProvider
    {
        private readonly ISpentOutputRepository _spentOutputRepository;

        public UnspentCoinsProvider(
            ISpentOutputRepository spentOutputRepository
        )
        {
            _spentOutputRepository = spentOutputRepository;
        }

        public async Task<IEnumerable<Coin>> FilterAsync(IList<Coin> coins)
        {
            var spentOutputs = new HashSet<OutPoint>(
                (await _spentOutputRepository.GetSpentOutputsAsync(coins.Select(o => new Output(o.Outpoint))))
                .Select(o => new OutPoint(uint256.Parse(o.TransactionHash), o.N)));
            return coins.Where(c => !spentOutputs.Contains(c.Outpoint));
        }

        public async Task<IEnumerable<ColoredCoin>> FilterAsync(IList<ColoredCoin> coins)
        {
            var spentOutputs = new HashSet<OutPoint>(
                (await _spentOutputRepository.GetSpentOutputsAsync(coins.Select(o => new Output(o.Outpoint))))
                .Select(o => new OutPoint(uint256.Parse(o.TransactionHash), o.N)));
            return coins.Where(c => !spentOutputs.Contains(c.Outpoint));
        }
    }
}
