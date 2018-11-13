﻿using System.Linq;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Service.Bitcoin.Api.Core.Constants;
using Lykke.Service.Bitcoin.Api.Core.Domain.Wallet;
using Lykke.Service.Bitcoin.Api.Core.Services;
using Lykke.Service.Bitcoin.Api.Core.Services.Asset;
using Lykke.Service.Bitcoin.Api.Core.Services.BlockChainReaders;
using Lykke.Service.Bitcoin.Api.Core.Services.Pagination;
using NBitcoin;

namespace Lykke.Service.Bitcoin.Api.Services.Wallet
{
    public class WalletBalanceService : IWalletBalanceService
    {
        private readonly IAssetService _assetService;
        private readonly IWalletBalanceRepository _balanceRepository;
        private readonly IBlockChainProvider _blockChainProvider;
        private readonly ILog _log;
        private readonly Network _network;
        private readonly IObservableWalletRepository _observableWalletRepository;

        public WalletBalanceService(IWalletBalanceRepository balanceRepository,
            IObservableWalletRepository observableWalletRepository,
            IBlockChainProvider blockChainProvider,
            IAssetService assetService,
            Network network,
            ILogFactory logFactory)
        {
            _balanceRepository = balanceRepository;
            _observableWalletRepository = observableWalletRepository;
            _blockChainProvider = blockChainProvider;
            _assetService = assetService;
            _network = network;
            _log = logFactory.CreateLog(this);
        }

        public async Task SubscribeAsync(string address)
        {
            await _observableWalletRepository.InsertAsync(ObservableWallet.Create(address));
        }

        public async Task UnsubscribeAsync(string address)
        {
            await _observableWalletRepository.DeleteAsync(address);
            foreach (var asset in await _assetService.GetAllAssetsAsync())
                await _balanceRepository.DeleteIfExistAsync(address, asset.AssetId);
        }

        public async Task<IPaginationResult<IWalletBalance>> GetBalancesAsync(int take, string continuation)
        {
            return await _balanceRepository.GetBalancesAsync(take, continuation);
        }

        public async Task<IWalletBalance> UpdateBtcBalanceAsync(string address, int minConfirmations)
        {
            var wallet = await _observableWalletRepository.GetAsync(address);
            if (wallet != null)
            {
                var lastBlock = await _blockChainProvider.GetLastBlockHeightAsync();
                return await UpdateBitcoinBalance(wallet, lastBlock, minConfirmations);
            }

            return null;
        }

        private async Task UpdateBalanceAsync(IObservableWallet wallet, int minConfirmations)
        {
            var lastBlock = await _blockChainProvider.GetLastBlockHeightAsync();

            _log.Info("Updating balance of address", context: new { wallet.Address, height = lastBlock});

            await UpdateBitcoinBalance(wallet, lastBlock, minConfirmations);
            await UpdateColoredBalance(wallet, lastBlock, minConfirmations);
        }

        public async Task UpdateBalanceAsync(string address, int minConfirmations)
        {
            var wallet = await _observableWalletRepository.GetAsync(address);
            if (wallet != null)
            {
                await UpdateBalanceAsync(wallet, minConfirmations);
            }
            else
            {
                _log.Info("Address not added to observable list. Updating balance skipped", context: address);
            }
        }

        private async Task<IWalletBalance> UpdateBitcoinBalance(IObservableWallet wallet, int height,
            int minConfirmations)
        {
            var balance =
                await _blockChainProvider.GetBalanceSatoshiFromUnspentOutputsAsync(wallet.Address, minConfirmations);
            
            if (balance != 0)
            {
                var walletBalanceEntity =
                    WalletBalance.Create(wallet.Address, balance, height, Constants.Assets.Bitcoin.AssetId);
                await _balanceRepository.InsertOrReplaceAsync(walletBalanceEntity);
                return walletBalanceEntity;
            }

            await _balanceRepository.DeleteIfExistAsync(wallet.Address, Constants.Assets.Bitcoin.AssetId);
            return null;
        }

        private async Task UpdateColoredBalance(IObservableWallet wallet, int height, int minConfirmations)
        {
            var coloredCoins =
                await _blockChainProvider.GetColoredUnspentOutputsAsync(wallet.Address, minConfirmations);

            if (!coloredCoins.Any())
                return;

            var coloredAssets = await _assetService.GetColoredAssetsAsync();

            foreach (var coinsGroup in coloredCoins.GroupBy(o => o.AssetId))
            {
                var blockchainAssetId = coinsGroup.Key.GetWif(_network).ToWif();

                var coloredAsset = coloredAssets.FirstOrDefault(o => o.BlockchainAssetId == blockchainAssetId);

                if (coloredAsset == null)
                {
                    _log.Warning($"Detected unknown colored asset deposit on address {wallet.Address}");
                    continue;
                }

                var balance = coinsGroup.Sum(o => o.Amount.Quantity);
                var walletBalanceEntity = WalletBalance.Create(wallet.Address, balance, height, coloredAsset.AssetId);

                await _balanceRepository.InsertOrReplaceAsync(walletBalanceEntity);
            }
        }
    }
}
