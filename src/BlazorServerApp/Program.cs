using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR().AddAzureSignalR(options =>
{
    options.ConnectionString = builder.Configuration.GetValue<string>("AzureSignalRConnectionString");
});

var BlobStorageUri = builder.Configuration.GetValue<string>("BlobStorageUri");
var KeyVaultURI = builder.Configuration.GetValue<string>("KeyVaultURI");
builder.Services.AddAzureClientsCore();

var cred = new DefaultAzureCredential();

builder.Services.AddDataProtection()
    .PersistKeysToAzureBlobStorage(new Uri(BlobStorageUri), cred)
    .ProtectKeysWithAzureKeyVault(new Uri(KeyVaultURI), cred);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.Run();
