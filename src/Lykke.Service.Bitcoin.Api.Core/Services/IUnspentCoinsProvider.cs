using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;

namespace Lykke.Service.Bitcoin.Api.Core.Services
{
    public interface IUnspentCoinsProvider
    {
        Task<IEnumerable<Coin>> FilterAsync(IList<Coin> coins);
        Task<IEnumerable<ColoredCoin>> FilterAsync(IList<ColoredCoin> coins);
    }
}
