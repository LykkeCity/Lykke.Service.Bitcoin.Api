﻿using System;
using Common;
using Lykke.AzureStorage.Tables;
using Lykke.Service.Bitcoin.Api.Core.Domain.Transactions;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Service.Bitcoin.Api.AzureRepositories.Transactions
{
    public class UnconfirmedTransactionEntity : AzureTableEntity, IUnconfirmedTransaction
    {
        public string TxHash { get; set; }
        public Guid OperationId { get; set; }

        public static string GeneratePartitionKey(Guid operationId)
        {
            return operationId.ToString().CalculateHexHash32(3);
        }

        public static string GenerateRowKey(Guid operationId)
        {
            return operationId.ToString();
        }

        public static UnconfirmedTransactionEntity Create(IUnconfirmedTransaction source)
        {
            return new UnconfirmedTransactionEntity
            {
                PartitionKey = GeneratePartitionKey(source.OperationId),
                RowKey = GenerateRowKey(source.OperationId),
                OperationId = source.OperationId,
                TxHash = source.TxHash
            };
        }
    }
}
