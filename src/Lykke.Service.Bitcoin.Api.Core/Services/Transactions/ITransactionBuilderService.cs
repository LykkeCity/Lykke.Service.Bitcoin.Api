using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Service.Bitcoin.Api.Core.Domain.Operation;

namespace Lykke.Service.Bitcoin.Api.Core.Services.Transactions
{
    public interface ITransactionBuilderService
    {
        Task<IBuiltTransaction> GetManyOutputsTransferTransactionAsync(OperationInput fromAddress,
            IList<OperationOutput> toAddresses,
            bool includeFee);

        Task<IBuiltTransaction> GetManyInputsTransferTransactionAsync(IList<OperationInput> fromAddresses,
            OperationOutput toAddress,
            bool includeFee);

        Task<IBuiltTransaction> GetTransferTransactionAsync(OperationInput fromAddress, OperationOutput toAddress,
            bool includeFee);
    }
}
