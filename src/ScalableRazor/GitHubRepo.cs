using System.Text.Json.Serialization;

namespace ScalableRazor
{
    [GenerateSerializer]
    public class GitHubRepo
    {
        [JsonPropertyName("name")]
        [Id(0)]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        [Id(1)]
        public string Description { get; set; }

        [JsonPropertyName("html_url")]
        [Id(2)]
        public string HtmlUrl { get; set; }
    }
}
