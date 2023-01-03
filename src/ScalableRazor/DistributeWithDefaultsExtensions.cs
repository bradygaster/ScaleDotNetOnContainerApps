namespace ScalableRazor
{
    public static class DistributeWithDefaultsExtensions
    {
        public static WebApplicationBuilder AddDistributedDefaults(this WebApplicationBuilder builder)
        {
            var storageConnectionString = string.IsNullOrEmpty(builder.Configuration.GetValue<string>("AZURE_STORAGE_CONNECTION_STRING"))
                                        ? "UseDevelopmentStorage=true"
                                        : builder.Configuration.GetValue<string>("AZURE_STORAGE_CONNECTION_STRING");

            builder.Services.AddOrleans(siloBuilder =>
            {
                siloBuilder
                    .UseAzureStorageClustering(azureStorageConfigurationOptions =>
                    {
                        azureStorageConfigurationOptions.ConfigureTableServiceClient(storageConnectionString);
                    })
                    .AddAzureTableGrainStorageAsDefault(azureBlobGrainStorageOptions =>
                    {
                        azureBlobGrainStorageOptions.ConfigureTableServiceClient(storageConnectionString);
                    });
            });

            builder.Services.AddOrleansDistributedCache(options =>
            {
                options.PersistWhenSet = true;
                options.DefaultDelayDeactivation = TimeSpan.FromMinutes(5);
            });

            builder.Services.AddDataProtection()
                            .PersistKeysToOrleans();

            return builder;
        }
    }
}
