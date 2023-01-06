using Microsoft.AspNetCore.DataProtection;

namespace ScalableRazor
{
    public static class DistributeWithDefaultsExtensions
    {
        public static WebApplicationBuilder AddDistributedDefaults(this WebApplicationBuilder builder)
        {
            if (builder.Configuration.GetValue<bool>("ASPNETCORE_DISTRIBUTED"))
            {
                // add distributed caching
                builder.Services.AddOrleansDistributedCache(options =>
                {
                    options.PersistWhenSet = true;
                    options.DefaultDelayDeactivation = TimeSpan.FromMinutes(5);
                });

                // add data protection
                builder.Services.AddDataProtection()
                                .PersistKeysToOrleans();
            }

            return builder;
        }
    }
}
