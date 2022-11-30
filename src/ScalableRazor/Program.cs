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
        });
});

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
