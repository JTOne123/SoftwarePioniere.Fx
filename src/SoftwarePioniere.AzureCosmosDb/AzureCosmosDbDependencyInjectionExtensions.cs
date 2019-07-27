﻿using System;
using Foundatio.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftwarePioniere.AzureCosmosDb;
using SoftwarePioniere.ReadModel;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    public static class AzureCosmosDbDependencyInjectionExtensions
    {
      
        public static IServiceCollection AddAzureCosmosDbEntityStore(this IServiceCollection services, Action<AzureCosmosDbOptions> configureOptions)
        {

            services
                .AddOptions()
                .Configure(configureOptions);

            services
                .AddSingleton<AzureCosmosDbConnectionProvider>()
                .AddSingleton<IEntityStoreConnectionProvider>(provider => provider.GetRequiredService<AzureCosmosDbConnectionProvider>())
                .AddSingleton<IEntityStore>(provider =>
                {
                    var options = provider.GetRequiredService<IOptions<AzureCosmosDbOptions>>().Value;
                    options.CacheClient = provider.GetRequiredService<ICacheClient>();
                    options.LoggerFactory = provider.GetRequiredService<ILoggerFactory>();

                    return new AzureCosmosDbEntityStore(options, provider.GetRequiredService<AzureCosmosDbConnectionProvider>());
                });

            return services;
        }
    }
}
