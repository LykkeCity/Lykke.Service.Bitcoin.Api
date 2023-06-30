using System;
using System.Collections.Generic;
using Lykke.Service.Bitcoin.Api.Services.BlockChainProviders;
using Lykke.SettingsReader.Attributes;

namespace Lykke.Service.Bitcoin.Api.Settings.ServiceSettings
{
    public class BitcoinApiSettings
    {
        public DbSettings Db { get; set; }
        public string Network { get; set; }

        [HttpCheck("/")]
        public string NinjaApiUrl { get; set; }

        [Optional]
        public int MinConfirmationsToDetectOperation { get; set; } = 3;

        [Optional]
        public int FeePerByte { get; set; } = 1;

        [Optional]
        public int MaxFeePerByte { get; set; } = 200;

        [Optional]
        public int MinFeePerByte { get; set; } = 1;

        [Optional]
        public int FeeConfirmationTargetInBlocks { get; set; } = 3;

        public RpcClientSettings Rpc { get; set; }

        public int StartFromBlockHeight { get; set; }

        public int IgnoreUnspentOutputsBeforeBlockHeight { get; set; }

        public string HotWalletAddress { get; set; }

        [Optional]
        public IReadOnlyList<Guid> OperationsToForceRebuild { get; set; }
        
        [Optional]
        public bool UseRpcBlockchainProvider { get; set; }
    }
}
