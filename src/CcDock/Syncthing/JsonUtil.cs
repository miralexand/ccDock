using System.Text.Json.Nodes;

namespace CcDock.Syncthing;

internal static class JsonUtil
{
    public static string GetString(JsonNode? node, string property, string fallback = "")
    {
        var value = node?[property];
        if (value is null)
        {
            return fallback;
        }

        try
        {
            return value.GetValue<string>() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public static long GetLong(JsonNode? node, string property, long fallback = 0)
    {
        var value = node?[property];
        if (value is null)
        {
            return fallback;
        }

        try
        {
            return value.GetValue<long>();
        }
        catch
        {
            return fallback;
        }
    }

    public static int GetInt(JsonNode? node, string property, int fallback = 0)
    {
        var value = node?[property];
        if (value is null)
        {
            return fallback;
        }

        try
        {
            return value.GetValue<int>();
        }
        catch
        {
            return fallback;
        }
    }

    public static bool GetBool(JsonNode? node, string property, bool fallback = false)
    {
        var value = node?[property];
        if (value is null)
        {
            return fallback;
        }

        try
        {
            return value.GetValue<bool>();
        }
        catch
        {
            return fallback;
        }
    }

    public static JsonArray? GetArray(JsonNode? node, string property)
        => node?[property] as JsonArray;

    public static JsonObject? GetObject(JsonNode? node, string property)
        => node?[property] as JsonObject;

    public static IEnumerable<JsonNode?> Children(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var pair in obj)
            {
                yield return pair.Value;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                yield return item;
            }
        }
    }
}
