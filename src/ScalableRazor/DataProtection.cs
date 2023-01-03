using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Orleans.Runtime;
using System.Collections.ObjectModel;
using System.Xml.Linq;

namespace ScalableRazor
{
    public static class DataProtection
    {
        public static IDataProtectionBuilder PersistKeysToOrleans(this IDataProtectionBuilder builder)
        {
            builder.Services.AddSingleton<OrleansXmlRepository>();
            builder.Services.AddOptions<KeyManagementOptions>().Configure<OrleansXmlRepository>((options, repository) =>
            {
                options.XmlRepository = repository;
            });

            return builder;
        }
    }

    public class OrleansXmlRepository : IXmlRepository
    {
        private static readonly XName RepositoryElementName = "repository";
        private IGrainFactory _grainFactory;
        private readonly ILogger<OrleansXmlRepository> _logger;

        public OrleansXmlRepository(IGrainFactory grainFactory, ILogger<OrleansXmlRepository> logger)
        {
            _grainFactory = grainFactory;
            _logger = logger;
        }

        public IReadOnlyCollection<XElement> GetAllElements()
            => new ReadOnlyCollection<XElement>(
                Task.Run(() => GetAllElementsAsync()).GetAwaiter().GetResult()
            );

        public void StoreElement(XElement element, string friendlyName)
            => Task.Run(() => StoreElementAsync(element)).GetAwaiter().GetResult();

        private async Task<IList<XElement>> GetAllElementsAsync()
        {
            var grain = _grainFactory.GetGrain<IOrleansXmlRepositoryGrain>(Guid.Empty);

            try
            {
                var xml = await grain.GetKey();

                if (xml == null)
                {
                    return new List<XElement>();
                }
                else
                {
                    var result = new List<XElement> { XElement.Parse(xml) };
                    return result;
                }
            }
            catch
            {
                return new List<XElement>();
            }
        }

        private async Task StoreElementAsync(XElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            
            var rdr = element.CreateReader();
            rdr.MoveToContent();
            var xml = rdr.ReadOuterXml();

            try
            {
                var grain = _grainFactory.GetGrain<IOrleansXmlRepositoryGrain>(Guid.Empty);
                await grain.StoreKey(xml);
            }
            catch (NullReferenceException)
            {
                _logger.LogCritical("Orleans isn't up yet.");
            }
        }
    }

    public interface IOrleansXmlRepositoryGrain : IGrainWithGuidKey
    {
        Task StoreKey(string xml);
        Task<string> GetKey();
    }

    public class OrleansXmlRepositoryGrain : Grain, IOrleansXmlRepositoryGrain
    {
        private readonly IPersistentState<XmlRepositoryItem> _state;
        private readonly ILogger<OrleansXmlRepositoryGrain> _logger;

        public OrleansXmlRepositoryGrain(
            [PersistentState("Keys")]
            IPersistentState<XmlRepositoryItem> state, ILogger<OrleansXmlRepositoryGrain> logger)
        {
            _state = state;
            _logger = logger;
        }

        public async Task<string> GetKey()
        {
            await _state.ReadStateAsync();
            return _state.State.Value;
        }

        public async Task StoreKey(string xml)
        {
            _logger.LogInformation(xml);
            
            _state.State.Value = xml;
            await _state.WriteStateAsync();
        }
    }

    [GenerateSerializer]
    public class XmlRepositoryItem
    {
        [Id(0)]
        public string Value { get; set; }
    }
}
