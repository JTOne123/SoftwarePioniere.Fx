﻿using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SoftwarePioniere.Domain;

namespace SoftwarePioniere.EventStore.Domain
{
    public class EventStoreProjectionByCategoryInitializer : IEventStoreInitializer
    {
        private readonly IEventStoreSetup _setup;

        private readonly ILogger _logger;

        public EventStoreProjectionByCategoryInitializer(ILoggerFactory loggerFactory
            , IEventStoreSetup setup)
        {
            _setup = setup;

            _logger = loggerFactory.CreateLogger(GetType());
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            _logger.LogTrace("InitializeAsync");

            if (!await _setup.CheckProjectionIsRunningAsync("$by_category").ConfigureAwait(false))
            {
                _logger.LogInformation("Enabling $by_category Projection");
                await _setup.EnableProjectionAsync("$by_category").ConfigureAwait(false);
            }
        }

        public int ExecutionOrder { get; } = (int.MinValue + 1);
    }
}
