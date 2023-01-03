using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Azure;
using ScalableRazor;

var builder = WebApplication.CreateBuilder(args);

var storageConnectionString = string.IsNullOrEmpty(builder.Configuration.GetValue<string>("AZURE_STORAGE_CONNECTION_STRING"))
    ? "UseDevelopmentStorage=true"
    : builder.Configuration.GetValue<string>("AZURE_STORAGE_CONNECTION_STRING");

builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddServerSideBlazor();
builder.Services.AddSession();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddSingleton<FavoritesService>();

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
        })
        //.ConfigureServices(services =>
        //{
        //    var BlobStorageUri = builder.Configuration["AzureURIs:BlobStorage"];
        //    var KeyVaultURI = builder.Configuration["AzureURIs:KeyVault"];
        //    var azureCredential = new DefaultAzureCredential();
        //    builder.Services.AddAzureClientsCore();
        //    builder.Services.AddDataProtection()
        //                    .PersistKeysToAzureBlobStorage(new Uri(BlobStorageUri), azureCredential)
        //                    .ProtectKeysWithAzureKeyVault(new Uri(KeyVaultURI), azureCredential)
        //                    ;
            
        //})
        ;
});



builder.Services.AddOrleansDistributedCache(options =>
{
    options.PersistWhenSet = true;
    options.DefaultDelayDeactivation = TimeSpan.FromMinutes(5);
});

builder.Services.AddDataProtection()
                .PersistKeysToOrleans();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseSession();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages(); 
app.MapGet("http400", () => Results.StatusCode(400));
app.Run();
