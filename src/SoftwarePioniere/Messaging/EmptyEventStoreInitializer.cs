﻿using System.Threading;
using System.Threading.Tasks;
using SoftwarePioniere.Domain;

namespace SoftwarePioniere.Messaging
{
    public class EmptyEventStoreInitializer : IEventStoreInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.CompletedTask;
        }

        public int ExecutionOrder { get; } = int.MaxValue;
    }
}
