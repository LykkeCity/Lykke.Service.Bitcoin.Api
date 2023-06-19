using System;
using System.Net;
using Autofac;
using Autofac.Features.AttributeFilters;
using Lykke.Job.Bitcoin.Settings;
using Lykke.Job.Bitcoin.Settings.ServiceSettings;
using Lykke.Service.Bitcoin.Api.Core.Services;
using Lykke.Service.Bitcoin.Api.Core.Services.Address;
using Lykke.Service.Bitcoin.Api.Core.Services.Asset;
using Lykke.Service.Bitcoin.Api.Core.Services.BlockChainReaders;
using Lykke.Service.Bitcoin.Api.Core.Services.Fee;
using Lykke.Service.Bitcoin.Api.Services.Address;
using Lykke.Service.Bitcoin.Api.Services.Asset;
using Lykke.Service.Bitcoin.Api.Services.BlockChainProviders;
using Lykke.Service.Bitcoin.Api.Services.BlockChainProviders.QbitNinja;
using Lykke.Service.Bitcoin.Api.Services.Fee;
using Lykke.Service.Bitcoin.Api.Services.ObservableOperation;
using Lykke.Service.Bitcoin.Api.Services.Operations;
using Lykke.Service.Bitcoin.Api.Services.Wallet;
using Lykke.SettingsReader;
using NBitcoin;
using NBitcoin.RPC;
using QBitNinja.Client;

namespace Lykke.Job.Bitcoin.Modules
{
    public class ServiceModule : Module
    {
        private readonly BitcoinJobSettings _settings;

        public ServiceModule(IReloadingManager<AppSettings> settings)
        {
            _settings = settings.CurrentValue.Bitcoin;
        }

        protected override void Load(ContainerBuilder builder)
        {
            RegisterNetwork(builder);
            RegisterAddressValidatorServices(builder);
            RegisterBlockChainReaders(builder);
            RegisterDetectorServices(builder);
            RegisterSpentOutputSettings(builder);
            RegisterObservableServices(builder);
            RegisterAssetServices(builder);
            RegisterFeeServices(builder);

            builder.RegisterType<UnspentCoinsProvider>().As<IUnspentCoinsProvider>();
        }

        private void RegisterNetwork(ContainerBuilder builder)
        {
            var network = Network.GetNetwork(_settings.Network);
            builder.RegisterInstance(network).As<Network>();
        }

        private void RegisterAddressValidatorServices(ContainerBuilder builder)
        {
            builder.RegisterType<AddressValidator>().As<IAddressValidator>().WithAttributeFiltering();
        }

        private void RegisterBlockChainReaders(ContainerBuilder builder)
        {
            var networkType = Network.GetNetwork(_settings.Network);

            builder.RegisterInstance(new QBitNinjaClient(_settings.NinjaApiUrl, networkType));
            if (_settings.UseRpcBlockchainProvider)
            {
                builder.RegisterType<RpcBlockchainProvider>().As<IBlockChainProvider>();
            }
            else
            {
                builder.RegisterType<NinjaApiBlockChainProvider>().As<IBlockChainProvider>();
            }
            builder.RegisterInstance(new RPCClient(
                new NetworkCredential(_settings.Rpc.UserName, _settings.Rpc.Password),
                new Uri(_settings.Rpc.Host),
                networkType));
        }

        private void RegisterDetectorServices(ContainerBuilder builder)
        {
            builder.RegisterInstance(new HotWalletAddressSettings
            {
                HotWalletAddress = _settings.HotWalletAddress
            });

            builder.RegisterInstance(new BlockHeightSettings
            {
                IgnoreUnspentOutputsBeforeBlockHeight = _settings.IgnoreUnspentOutputsBeforeBlockHeight,
                StartFromBlockHeight = _settings.StartFromBlockHeight
            });

            builder.RegisterInstance(new OperationsConfirmationsSettings
            {
                MinConfirmationsToDetectOperation = _settings.MinConfirmationsToDetectOperation
            });
        }

        private void RegisterSpentOutputSettings(ContainerBuilder builder)
        {
            builder.RegisterInstance(new SpentOutputsSettings
            {
                SpentOutputsExpirationDays = _settings.SpentOutputsExpirationDays
            });
        }

        private void RegisterFeeServices(ContainerBuilder builder)
        {
            builder.RegisterInstance(new FeeRateSettings
            {
                MinFeeRatePerByte = _settings.MinFeePerByte,
                MaxFeeRatePerByte = _settings.MaxFeePerByte,
                DefaultFeeRatePerByte = _settings.FeePerByte,
                FeeConfirmationTargetInBlocks = _settings.FeeConfirmationTargetInBlocks
            });

            builder.RegisterType<FeeRateFacade>()
                .As<IFeeRateFacade>();

            builder.RegisterType<FeeRateProvider>().As<IFeeRateProvider>().SingleInstance();
        }

        private void RegisterObservableServices(ContainerBuilder builder)
        {
            builder.RegisterType<ObservableOperationService>().As<IObservableOperationService>();
            builder.RegisterType<WalletBalanceService>().As<IWalletBalanceService>();
        }

        private void RegisterAssetServices(ContainerBuilder builder)
        {
            builder.RegisterType<AssetService>().As<IAssetService>();
        }
    }
}
