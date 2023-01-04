using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Orleans.Runtime;
using System.Collections.ObjectModel;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ScalableRazor
{
    public static class DataProtection
    {
        internal const string DataProtectionLogPrefix = "[ODPP]: ";
        
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
            _logger.LogInformation($"{DataProtection.DataProtectionLogPrefix}There are currently {doc.Root.Elements().Count()} keys in the ring.");
            return doc.Root.Elements().ToList();
        }

        private async Task StoreElementAsync(XElement element)
        {
            var xml = string.Empty;
            var keyId = Guid.Parse(element.Attribute("id").Value);

            var grain = _grainFactory.GetGrain<IOrleansXmlRepositoryGrain>(Guid.Empty);

            try
            {
                _logger.LogInformation($"{DataProtection.DataProtectionLogPrefix}Storing key {keyId} in Orleans.");
                await grain.StoreKey(keyId, element.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{DataProtection.DataProtectionLogPrefix}Error storing key {keyId} in Orleans: {ex.StackTrace}.");
                throw ex;
            }
        }

        private async Task<XDocument> CreateKeyListFromOrleans()
        {
            var xml = string.Empty;
            var grain = _grainFactory.GetGrain<IOrleansXmlRepositoryGrain>(Guid.Empty);

            try
            {
                xml = await grain.GetKeys();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{DataProtection.DataProtectionLogPrefix}Unable to retrieve keys from Orleans: {ex.StackTrace}");
                throw ex;
            }

            if (string.IsNullOrEmpty(xml))
            {
                return new XDocument(new XElement(RepositoryElementName));
            }

            using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var xmlReaderSettings = new XmlReaderSettings()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreProcessingInstructions = true,
            };

            using (var xmlReader = XmlReader.Create(memoryStream, xmlReaderSettings))
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
            
            if (!_state.State.Any())
            {
                return doc.ToString();
            }
            else
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
            try
            {
                _logger.LogInformation($"{DataProtection.DataProtectionLogPrefix}Checking for existing key {keyId}.");

                if (_state.State.Any(x => x.Id == keyId))
                {
                    _logger.LogInformation($"{DataProtection.DataProtectionLogPrefix}Key {keyId} already exists.");
                }
                else
                {
                    _logger.LogInformation($"{DataProtection.DataProtectionLogPrefix}Storing key {keyId}.");

                    _state.State.Add(new XmlRepositoryItem
                    {
                        Id = keyId,
                        Xml = xml
                    });

                    await _state.WriteStateAsync();

                    _logger.LogInformation($"{DataProtection.DataProtectionLogPrefix}Stored key {keyId}.");
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"{DataProtection.DataProtectionLogPrefix}Error storing key {keyId}: {ex.StackTrace}");
                throw ex;
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
