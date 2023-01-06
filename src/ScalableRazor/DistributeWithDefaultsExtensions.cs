using Microsoft.AspNetCore.DataProtection;

namespace ScalableRazor
{
    public static class DistributeWithDefaultsExtensions
    {
        public static WebApplicationBuilder AddDistributedDefaults(this WebApplicationBuilder builder)
        {
            var storageConnectionString = string.IsNullOrEmpty(builder.Configuration.GetValue<string>("AZURE_STORAGE_CONNECTION_STRING"))
                                        ? "UseDevelopmentStorage=true"
                                        : builder.Configuration.GetValue<string>("AZURE_STORAGE_CONNECTION_STRING");

            // wire up the orleans silo
            builder.Services.AddOrleans(siloBuilder =>
            {
                siloBuilder
                    .UseAzureStorageClustering(azureStorageConfigurationOptions =>
                    {
                        azureStorageConfigurationOptions.ConfigureTableServiceClient(storageConnectionString);
                    })
                    .AddAzureBlobGrainStorageAsDefault(azureBlobGrainStorageOptions =>
                    {
                        azureBlobGrainStorageOptions.ContainerName = "grainstorage";
                        azureBlobGrainStorageOptions.ConfigureBlobServiceClient(storageConnectionString);
                    });
            });

            // add distributed caching
            builder.Services.AddOrleansDistributedCache(options =>
            {
                options.PersistWhenSet = true;
                options.DefaultDelayDeactivation = TimeSpan.FromMinutes(5);
            });

            // add data protection
            builder.Services.AddDataProtection()
                            .PersistKeysToOrleans();

            return builder;
        }
    }
}
