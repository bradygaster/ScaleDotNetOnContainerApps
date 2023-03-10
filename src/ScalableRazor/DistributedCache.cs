/*
Most of the code in this file and these implementations is lifted from https://github.com/MV10/OrleansDistributedCache,
then upgraded to Orleans 7.0, with some modifications along the way. 

Appreciation to the author for the original implementation and excellent blog post
summarizing how to use the implementation. 
https://mcguirev10.com/2019/09/18/distributed-caching-with-microsoft-orleans.html

The original code is licensed under the Apache license.
*/

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Concurrency;

namespace ScalableRazor
{
    public interface IOrleansDistributedCacheGrain : IGrainWithStringKey
    {
        ValueTask Set([Immutable] byte[]? value, TimeSpan? delayDeactivation);
        ValueTask<Immutable<byte[]?>> Get();
        ValueTask Refresh();
        ValueTask Clear();
    }

    public class OrleansDistributedCacheOptions
    {
        public TimeSpan DefaultDelayDeactivation { get; set; } = TimeSpan.FromMinutes(5);
    }

    public class OrleansDistributedCacheGrain : Grain, IOrleansDistributedCacheGrain
    {
        private readonly OrleansDistributedCacheOptions _options;
        private TimeSpan _delayDeactivation = TimeSpan.Zero;
        private byte[]? _value;

        public OrleansDistributedCacheGrain(IOptions<OrleansDistributedCacheOptions> options)
        {
            _options = options.Value;
            _delayDeactivation = _options.DefaultDelayDeactivation;
        }

        public ValueTask Clear()
        {
            _value = null;
            DeactivateOnIdle();
            return default;
        }

        public ValueTask<Immutable<byte[]?>> Get() => new(_value.AsImmutable());

        public ValueTask Refresh()
        {
            DelayDeactivation(_delayDeactivation);
            return default;
        }

        public ValueTask Set([Immutable] byte[]? value, TimeSpan? delayDeactivation)
        {
            _value = value;
            _delayDeactivation = (delayDeactivation is { } delay && delay > TimeSpan.Zero) ? delay : _options.DefaultDelayDeactivation;
            DelayDeactivation(_delayDeactivation);
            return default;
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

        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            var created = DateTimeOffset.UtcNow;
            var expires = AbsoluteExpiration(created, options);
            var seconds = ExpirationSeconds(created, expires, options);
            await _grainFactory.GetGrain<IOrleansDistributedCacheGrain>(key).Set(value, TimeSpan.FromSeconds(seconds ?? 0));
        }

        public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
            => (await _grainFactory.GetGrain<IOrleansDistributedCacheGrain>(key).Get()).Value;

        public Task RefreshAsync(string key, CancellationToken token = default)
            => _grainFactory.GetGrain<IOrleansDistributedCacheGrain>(key).Refresh().AsTask();

        public Task RemoveAsync(string key, CancellationToken token = default)
            => _grainFactory.GetGrain<IOrleansDistributedCacheGrain>(key).Clear().AsTask();

        #endregion

        #region Sync Methods

        [Obsolete(Use_Async_Only_Message)]
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            => Task.Run(() => SetAsync(key, value, options));

        [Obsolete(Use_Async_Only_Message)]
        public byte[]? Get(string key)
            => Task.Run(() => GetAsync(key)).Result;

        [Obsolete(Use_Async_Only_Message)]
        public void Refresh(string key)
            => Task.Run(() => RefreshAsync(key)).Wait();

        [Obsolete(Use_Async_Only_Message)]
        public void Remove(string key)
            => Task.Run(() => RemoveAsync(key)).Wait();

        #endregion

        private static DateTimeOffset? AbsoluteExpiration(DateTimeOffset creationTime, DistributedCacheEntryOptions options)
            => options.AbsoluteExpirationRelativeToNow.HasValue ? creationTime + options.AbsoluteExpirationRelativeToNow : options.AbsoluteExpiration;

        private static long? ExpirationSeconds(DateTimeOffset creationTime, DateTimeOffset? absoluteExpiration, DistributedCacheEntryOptions options)
        {
            if (absoluteExpiration.HasValue && options.SlidingExpiration.HasValue)
            {
                return (long)Math.Min(
                    (absoluteExpiration.Value - creationTime).TotalSeconds,
                    options.SlidingExpiration.Value.TotalSeconds);
            }

            if (absoluteExpiration.HasValue)
                return (long)(absoluteExpiration.Value - creationTime).TotalSeconds;

            if (options.SlidingExpiration.HasValue)
                return (long)options.SlidingExpiration.Value.TotalSeconds;

            return null;
        }
    }
    public static class OrleansDistributedCacheExtensions
    {
        public static IServiceCollection AddOrleansDistributedCache(this IServiceCollection services, Action<OrleansDistributedCacheOptions>? options = null)
        {
            services.AddOptions();
            if (options is not null)
            {
                services.Configure(options);
            }

            services.AddSingleton<IDistributedCache, OrleansDistributedCache>();
            return services;
        }
    }
}
