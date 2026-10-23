using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using WechatMultiopen.Models;

namespace WechatMultiopen.Config;

public static class FeatureTableLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static FeatureTable Load()
    {
        var external = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "wechat-multiopen", "mutex_features.json");

        if (File.Exists(external))
        {
            try { return Parse(File.ReadAllText(external)); }
            catch { /* fall back to the embedded safe defaults */ }
        }

        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(x => x.EndsWith("mutex_features.json", StringComparison.OrdinalIgnoreCase));
        if (resource is null) return new FeatureTable("1.0", Array.Empty<BranchFeature>());
        using var stream = assembly.GetManifestResourceStream(resource);
        using var reader = new StreamReader(stream!);
        return Parse(reader.ReadToEnd());
    }

    private static FeatureTable Parse(string json)
    {
        var table = JsonSerializer.Deserialize<FeatureTable>(json, JsonOptions);
        if (table is null) throw new InvalidDataException("特征表为空");
        return table;
    }
}
