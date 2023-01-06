/*
Most of the code in this file and these implementations is lifted from https://github.com/MV10/OrleansDistributedCache,
then upgraded to Orleans 7.0, with some modifications along the way. 

Appreciation to the author for the original implementation and excellent blog post
summarizing how to use the implementation. 
https://mcguirev10.com/2019/09/18/distributed-caching-with-microsoft-orleans.html

The original code is licensed under the Apache license.
*/

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.Extensions.Distributed;
    
    public static class OrleansDistributedCacheExtensions
    {
        public static IServiceCollection AddOrleansDistributedCache(this IServiceCollection services, Action<OrleansDistributedCacheOptions> options = null)
        {
            services.AddOptions().Configure(options ?? new Action<OrleansDistributedCacheOptions>(defaultOptions => { }));
            services.AddSingleton<IDistributedCache, OrleansDistributedCache>();
            return services;
        }
    }
}

namespace Microsoft.Extensions.Distributed
{
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.Extensions.Options;
    using Orleans.Concurrency;

    public interface IOrleansDistributedCacheGrain<T> : IGrainWithStringKey
    {
        Task Set(Immutable<T> value, TimeSpan delayDeactivation);
        Task<Immutable<T>> Get();
        Task Refresh();
        Task Clear();
    }

    public class OrleansDistributedCacheOptions
    {
        public bool PersistWhenSet { get; set; } = true;
        public TimeSpan DefaultDelayDeactivation { get; set; } = TimeSpan.FromMinutes(5);
    }

    public class OrleansDistributedCacheGrain<T> : Grain<Immutable<T>>, IOrleansDistributedCacheGrain<T>
    {
        private TimeSpan delayDeactivation = TimeSpan.Zero;
        private readonly OrleansDistributedCacheOptions _options;
        private readonly ILogger<OrleansDistributedCacheGrain<T>> _logger;

        public OrleansDistributedCacheGrain(IOptions<OrleansDistributedCacheOptions> options, ILogger<OrleansDistributedCacheGrain<T>> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task Clear()
        {
            await base.ClearStateAsync();
            DeactivateOnIdle();
        }

        public Task<Immutable<T>> Get()
            => Task.FromResult(State);

        public async Task Refresh()
        {
            await base.ReadStateAsync();
            DelayDeactivation(delayDeactivation);
        }

        public async Task Set(Immutable<T> value, TimeSpan delayDeactivation)
        {
            State = value;
            this.delayDeactivation = (delayDeactivation > TimeSpan.Zero) ? delayDeactivation : _options.DefaultDelayDeactivation;

            if (_options.PersistWhenSet)
                await base.WriteStateAsync();
        }
    }
    public class OrleansDistributedCache : IDistributedCache
    {
        public const string OrleansDistributedCacheStorageProviderName = "OrleansDistributedCacheStorageProvider";

        private const string Use_Async_Only_Message = "OrleansDistributedCacheService only supports asynchronous operations";
        private readonly IGrainFactory _grainFactory;

        public OrleansDistributedCache(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory;
        }

        #region Async Methods

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            var created = DateTimeOffset.UtcNow;
            var expires = AbsoluteExpiration(created, options);
            var seconds = ExpirationSeconds(created, expires, options);
            return _grainFactory.GetGrain<IOrleansDistributedCacheGrain<byte[]>>(key).Set(new Immutable<byte[]>(value), TimeSpan.FromSeconds(seconds ?? 0));
        }

        public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
            => (await _grainFactory.GetGrain<IOrleansDistributedCacheGrain<byte[]>>(key).Get()).Value;

        public Task RefreshAsync(string key, CancellationToken token = default)
            => _grainFactory.GetGrain<IOrleansDistributedCacheGrain<byte[]>>(key).Refresh();

        public Task RemoveAsync(string key, CancellationToken token = default)
            => _grainFactory.GetGrain<IOrleansDistributedCacheGrain<byte[]>>(key).Clear();

        #endregion

        #region Sync Methods

        [Obsolete(Use_Async_Only_Message)]
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            => SyncOverAsync.Run(() => SetAsync(key, value, options));

        [Obsolete(Use_Async_Only_Message)]
        public byte[]? Get(string key)
            => SyncOverAsync.Run(() => GetAsync(key));

        [Obsolete(Use_Async_Only_Message)]
        public void Refresh(string key)
            => SyncOverAsync.Run(() => RefreshAsync(key));

        [Obsolete(Use_Async_Only_Message)]
        public void Remove(string key)
            => SyncOverAsync.Run(() => RemoveAsync(key));

        #endregion

        private static DateTimeOffset? AbsoluteExpiration(DateTimeOffset creationTime, DistributedCacheEntryOptions options)
            => options.AbsoluteExpirationRelativeToNow.HasValue ? creationTime + options.AbsoluteExpirationRelativeToNow : options.AbsoluteExpiration;

        private static long? ExpirationSeconds(DateTimeOffset creationTime, DateTimeOffset? absoulteExpiration, DistributedCacheEntryOptions options)
        {
            if (absoulteExpiration.HasValue && options.SlidingExpiration.HasValue)
            {
                return (long)Math.Min(
                    (absoulteExpiration.Value - creationTime).TotalSeconds,
                    options.SlidingExpiration.Value.TotalSeconds);
            }

            if (absoulteExpiration.HasValue)
                return (long)(absoulteExpiration.Value - creationTime).TotalSeconds;

            if (options.SlidingExpiration.HasValue)
                return (long)options.SlidingExpiration.Value.TotalSeconds;

            return null;
        }

        private static class SyncOverAsync
        {
            private static readonly TaskFactory factory
                = new TaskFactory(CancellationToken.None, TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);

            public static void Run(Func<Task> task)
                => factory.StartNew(task).Unwrap().GetAwaiter().GetResult();

            public static TResult Run<TResult>(Func<Task<TResult>> task)
                => factory.StartNew(task).Unwrap().GetAwaiter().GetResult();
        }
    }
}