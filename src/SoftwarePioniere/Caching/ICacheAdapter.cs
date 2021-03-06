﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Foundatio.Caching;
using SoftwarePioniere.ReadModel;

namespace SoftwarePioniere.Caching
{
    public interface ICacheAdapter
    {

        ICacheClient CacheClient { get; }

        Task<T> CacheLoad<T>(Func<Task<T>> loader, string cacheKey,
            int minutes = 120, bool setExpirationOnHit = true);

        //Task<T> CacheLoadItem<T>(Func<Task<T>> loader, string cacheKey,
        //    int minutes = 120);

        Task<T[]> CacheLoadItems<T>(Func<Task<IEnumerable<T>>> loader, string cacheKey, int minutes = 120, bool setExpirationOnHit = true);

        //Task<PagedResults<T>> CacheLoadPagedItems<T>(Func<Task<PagedResults<T>>> loader, string cacheKey,
        //    int minutes = 60, ILogger logger = null);

        Task<int> RemoveByPrefixAsync(string prefix);

        Task<bool> AddAsync<T>(string key, T value);

        Task<List<T>> LoadSetItems<T>(string setKey, Expression<Func<T, bool>> @where, int minutes = 120, CancellationToken cancellationToken = default)
            where T : Entity;

        Task<T[]> LoadListAndAddSetToCache<T>(string setKey, Expression<Func<T, bool>> @where, int minutes = 120, CancellationToken cancellationToken = default) where T : Entity;

        Task SetItemsEnsureAsync(string setKey, string entityId);

        Task SetItemsEnsureNotAsync(string setKey, string entityId);

        //Task LockPrefix(string prefix);

        //Task ReleasePrefix(string prefix);

    }

    public interface ICacheKeyBuilder
    {
        ICacheKeyBuilder WithPrefix(string prefix);

        //ICacheKeyBuilder Append(IEnumerable<string> values);

        ICacheKeyBuilder Append(string v1);

        //ICacheKeyBuilder Append(string v1, int i1, int i2);

        //ICacheKeyBuilder ForReadModel<T>()
    }
}
