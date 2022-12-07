using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Azure;

namespace ScalableRazor
{
    public static class DataProtection
    {
        public static void AddDataProtectionUsingBlobsAndKeyVault(this WebApplicationBuilder builder)
        {
            var BlobStorageUri = builder.Configuration["AzureURIs:BlobStorage"];
            var KeyVaultURI = builder.Configuration["AzureURIs:KeyVault"];
            var azureCredential = new DefaultAzureCredential();
            builder.Services.AddAzureClientsCore();
            builder.Services.AddDataProtection()
                            .PersistKeysToAzureBlobStorage(new Uri(BlobStorageUri), azureCredential)
                            .ProtectKeysWithAzureKeyVault(new Uri(KeyVaultURI), azureCredential);
        }
    }
}
