using System;
using System.Net;
using Autofac;
using Autofac.Features.AttributeFilters;
using Lykke.Common.Log;
using Lykke.Service.Bitcoin.Api.Core.Services;
using Lykke.Service.Bitcoin.Api.Core.Services.Address;
using Lykke.Service.Bitcoin.Api.Core.Services.Asset;
using Lykke.Service.Bitcoin.Api.Core.Services.BlockChainReaders;
using Lykke.Service.Bitcoin.Api.Core.Services.Broadcast;
using Lykke.Service.Bitcoin.Api.Core.Services.Fee;
using Lykke.Service.Bitcoin.Api.Core.Services.Operation;
using Lykke.Service.Bitcoin.Api.Core.Services.TransactionOutputs;
using Lykke.Service.Bitcoin.Api.Core.Services.Transactions;
using Lykke.Service.Bitcoin.Api.Services.Address;
using Lykke.Service.Bitcoin.Api.Services.Asset;
using Lykke.Service.Bitcoin.Api.Services.BlockChainProviders.QbitNinja;
using Lykke.Service.Bitcoin.Api.Services.Broadcast;
using Lykke.Service.Bitcoin.Api.Services.Fee;
using Lykke.Service.Bitcoin.Api.Services.ObservableOperation;
using Lykke.Service.Bitcoin.Api.Services.Operations;
using Lykke.Service.Bitcoin.Api.Services.TransactionOutputs;
using Lykke.Service.Bitcoin.Api.Services.Transactions;
using Lykke.Service.Bitcoin.Api.Services.Wallet;
using Lykke.Service.Bitcoin.Api.Settings;
using Lykke.Service.Bitcoin.Api.Settings.ServiceSettings;
using Lykke.SettingsReader;
using NBitcoin;
using NBitcoin.RPC;
using QBitNinja.Client;

namespace Lykke.Service.Bitcoin.Api.Modules
{
    public class ServiceModule : Module
    {
        private readonly BitcoinApiSettings _settings;

        public ServiceModule(IReloadingManager<AppSettings> settings)
        {
            _settings = settings.CurrentValue.Bitcoin;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterInstance(_settings);

            RegisterNetwork(builder);
            RegisterFeeServices(builder);
            RegisterAddressValidatorServices(builder);
            RegisterBlockChainReaders(builder);
            RegisterTransactionOutputsServices(builder);
            RegisterTransactionBuilderServices(builder);
            RegisterBroadcastServices(builder);
            RegisterObservableServices(builder);
            RegisterHistoryServices(builder);
            RegisterAssetServices(builder);


            builder.RegisterInstance(new HotWalletAddressSettings
            {
                HotWalletAddress = _settings.HotWalletAddress
            });

            builder.RegisterInstance(new BlockHeightSettings
            {
                IgnoreUnspentOutputsBeforeBlockHeight = _settings.IgnoreUnspentOutputsBeforeBlockHeight,
                StartFromBlockHeight = _settings.StartFromBlockHeight
            });

            builder.RegisterType<UnspentCoinsProvider>().As<IUnspentCoinsProvider>();
        }

        private void RegisterNetwork(ContainerBuilder builder)
        {
            var network = Network.GetNetwork(_settings.Network);
            builder.RegisterInstance(network).As<Network>();
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

            builder.RegisterType<FeeRateFacade>().As<IFeeRateFacade>();
            builder.RegisterType<FeeService>().As<IFeeService>();
        }

        private void RegisterAddressValidatorServices(ContainerBuilder builder)
        {
            builder.RegisterType<AddressValidator>().As<IAddressValidator>().WithAttributeFiltering();
        }

        private void RegisterBlockChainReaders(ContainerBuilder builder)
        {
            var networkType = Network.GetNetwork(_settings.Network);

            builder.RegisterInstance(new QBitNinjaClient(_settings.NinjaApiUrl, networkType));
            builder.RegisterType<NinjaApiBlockChainProvider>().As<IBlockChainProvider>();

            builder.RegisterInstance(new RPCClient(
                new NetworkCredential(_settings.Rpc.UserName, _settings.Rpc.Password),
                new Uri(_settings.Rpc.Host),
                networkType));
        }

        private void RegisterTransactionOutputsServices(ContainerBuilder builder)
        {
            builder.RegisterType<TransactionOutputsService>().As<ITransactionOutputsService>();
        }

        private void RegisterTransactionBuilderServices(ContainerBuilder builder)
        {
            builder.RegisterInstance(new OperationsConfirmationsSettings
            {
                MinConfirmationsToDetectOperation = _settings.MinConfirmationsToDetectOperation
            });
            builder.RegisterType<TransactionBuilderService>().As<ITransactionBuilderService>();
            builder.RegisterType<OperationService>().As<IOperationService>();
        }

        private void RegisterBroadcastServices(ContainerBuilder builder)
        {
            builder.RegisterType<BroadcastService>().As<IBroadcastService>();
        }

        private void RegisterObservableServices(ContainerBuilder builder)
        {
            builder.RegisterType<ObservableOperationService>().As<IObservableOperationService>();
            builder.RegisterType<WalletBalanceService>().As<IWalletBalanceService>();
        }

        private void RegisterHistoryServices(ContainerBuilder builder)
        {
            builder.RegisterType<HistoryService>().As<IHistoryService>();
        }

        private void RegisterAssetServices(ContainerBuilder builder)
        {
            builder.RegisterType<AssetService>().As<IAssetService>();
        }
    }
}
