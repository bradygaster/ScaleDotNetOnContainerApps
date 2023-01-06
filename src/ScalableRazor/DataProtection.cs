
namespace Microsoft.AspNetCore.DataProtection
{
    using Microsoft.AspNetCore.DataProtection.KeyManagement;
    using Microsoft.Extensions.Distributed;
    
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
}

namespace Microsoft.Extensions.Distributed
{
    using Microsoft.AspNetCore.DataProtection.Repositories;
    using Orleans.Runtime;
    using System.Collections.ObjectModel;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;

    public class OrleansXmlRepository : IXmlRepository
    {
        private IGrainFactory _grainFactory;
        private readonly ILogger<OrleansXmlRepository> _logger;
        private static readonly XName RepositoryElementName = "repository";

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
            var doc = await CreateKeyListFromOrleans();
            return doc.Root.Elements().ToList();
        }

        private async Task StoreElementAsync(XElement element)
            => await _grainFactory
                        .GetGrain<IOrleansXmlRepositoryGrain>(Guid.Empty)
                        .StoreKey(Guid.Parse(element.Attribute("id").Value), element.ToString());

        private async Task<XDocument> CreateKeyListFromOrleans()
        {
            var xml = string.Empty;

            try
            {
                xml = await _grainFactory.GetGrain<IOrleansXmlRepositoryGrain>(Guid.Empty).GetKeys();
            }
            catch (Exception ex)
            {
                throw ex;
            }

            if (string.IsNullOrEmpty(xml))
            {
                return new XDocument(new XElement(RepositoryElementName));
            }

            using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(xml));


            using (var xmlReader = XmlReader.Create(memoryStream, new XmlReaderSettings()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreProcessingInstructions = true,
            }))
            {
                return XDocument.Load(xmlReader);
            }
        }
    }

    public interface IOrleansXmlRepositoryGrain : IGrainWithGuidKey
    {
        Task StoreKey(Guid keyId, string xml);
        Task<string> GetKeys();
    }

    public class OrleansXmlRepositoryGrain : Grain, IOrleansXmlRepositoryGrain
    {
        private readonly IPersistentState<List<XmlRepositoryItem>> _state;
        private readonly ILogger<OrleansXmlRepositoryGrain> _logger;
        private static readonly XName RepositoryElementName = "repository";

        public OrleansXmlRepositoryGrain(
            [PersistentState("Keys")]
            IPersistentState<List<XmlRepositoryItem>> state, ILogger<OrleansXmlRepositoryGrain> logger)
        {
            _state = state;
            _logger = logger;
        }

        public async Task<string> GetKeys()
        {
            await _state.ReadStateAsync();
            var doc = new XDocument(new XElement(RepositoryElementName));
            
            if (_state.State.Any())
            {
                foreach (var item in _state.State)
                {
                    doc.Root.Add(XElement.Parse(item.Xml));
                }
            }

            var result = doc.ToString();
            return result;
        }

        public async Task StoreKey(Guid keyId, string xml)
        {
            if (!_state.State.Any(x => x.Id == keyId))
            {
                _state.State.Add(new XmlRepositoryItem
                {
                    Id = keyId,
                    Xml = xml
                });

                await _state.WriteStateAsync();
            }
        }
    }

    [GenerateSerializer]
    public class XmlRepositoryItem
    {
        [Id(0)]
        public Guid Id { get; set; }

        [Id(1)]
        public string Xml { get; set; }
    }
}
