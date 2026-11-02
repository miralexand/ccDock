using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CcDock.Syncthing;

/// <summary>
/// Small typed facade over the Syncthing REST API. The API key is only sent
/// to the loopback GUI address of the ccDock-owned Syncthing instance.
/// </summary>
public sealed class SyncthingClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly Uri _baseAddress;
    private bool _disposed;

    public SyncthingClient(string baseAddress, string apiKey)
    {
        var normalized = baseAddress.Trim();
        if (!normalized.Contains("://", StringComparison.Ordinal))
        {
            normalized = "http://" + normalized;
        }

        normalized = normalized
            .Replace("0.0.0.0", "127.0.0.1", StringComparison.Ordinal)
            .Replace("[::]", "[::1]", StringComparison.Ordinal);

        _baseAddress = new Uri(normalized);
        var handler = new HttpClientHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false
        };

        _http = new HttpClient(handler)
        {
            BaseAddress = _baseAddress,
            Timeout = TimeSpan.FromSeconds(25)
        };
        _http.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ccDock", "1.0"));
    }

    public string BaseAddress => _baseAddress.GetLeftPart(UriPartial.Authority);

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await _http.GetAsync("/rest/system/status", cts.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<JsonObject> GetSystemStatusAsync(CancellationToken cancellationToken = default)
        => await GetObjectAsync("/rest/system/status", cancellationToken).ConfigureAwait(false);

    public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var version = await GetObjectAsync("/rest/system/version", cancellationToken).ConfigureAwait(false);
        return JsonUtil.GetString(version, "version", "unknown");
    }

    public async Task<JsonObject> GetConfigAsync(CancellationToken cancellationToken = default)
        => await GetObjectAsync("/rest/config", cancellationToken).ConfigureAwait(false);

    public async Task<JsonObject> GetDeviceDefaultsAsync(CancellationToken cancellationToken = default)
        => await GetObjectAsync("/rest/config/defaults/device", cancellationToken).ConfigureAwait(false);

    public async Task<JsonObject> GetFolderDefaultsAsync(CancellationToken cancellationToken = default)
        => await GetObjectAsync("/rest/config/defaults/folder", cancellationToken).ConfigureAwait(false);

    public async Task<JsonNode?> GetDevicesAsync(CancellationToken cancellationToken = default)
        => await GetNodeAsync("/rest/config/devices", cancellationToken).ConfigureAwait(false);

    public async Task<JsonNode?> GetFoldersAsync(CancellationToken cancellationToken = default)
        => await GetNodeAsync("/rest/config/folders", cancellationToken).ConfigureAwait(false);

    public async Task<JsonObject> GetConnectionsAsync(CancellationToken cancellationToken = default)
        => await GetObjectAsync("/rest/system/connections", cancellationToken).ConfigureAwait(false);

    public async Task<JsonObject> GetFolderStatusAsync(string folderId, CancellationToken cancellationToken = default)
        => await GetObjectAsync($"/rest/db/status?folder={Uri.EscapeDataString(folderId)}", cancellationToken)
            .ConfigureAwait(false);

    public async Task<JsonObject> GetCompletionAsync(
        string folderId, string deviceId, CancellationToken cancellationToken = default)
        => await GetObjectAsync(
                $"/rest/db/completion?folder={Uri.EscapeDataString(folderId)}&device={Uri.EscapeDataString(deviceId)}",
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<JsonObject> GetPendingDevicesAsync(CancellationToken cancellationToken = default)
        => await GetObjectAsync("/rest/cluster/pending/devices", cancellationToken).ConfigureAwait(false);

    public async Task<JsonObject> GetPendingFoldersAsync(CancellationToken cancellationToken = default)
        => await GetObjectAsync("/rest/cluster/pending/folders", cancellationToken).ConfigureAwait(false);

    public Task PutDeviceAsync(string deviceId, JsonObject device, CancellationToken cancellationToken = default)
        => PutAsync($"/rest/config/devices/{Uri.EscapeDataString(deviceId)}", device, cancellationToken);

    public Task PutFolderAsync(string folderId, JsonObject folder, CancellationToken cancellationToken = default)
        => PutAsync($"/rest/config/folders/{Uri.EscapeDataString(folderId)}", folder, cancellationToken);

    public Task DeleteFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => SendNoContentAsync(
            HttpMethod.Delete,
            $"/rest/config/folders/{Uri.EscapeDataString(folderId)}",
            null,
            cancellationToken);

    public async Task IgnorePendingDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        await SendNoContentAsync(
                HttpMethod.Delete,
                $"/rest/cluster/pending/devices?device={Uri.EscapeDataString(deviceId)}",
                null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await _http.PostAsync("/rest/system/shutdown", content: null, cts.Token)
                .ConfigureAwait(false);
            // The server may close the connection before replying.
            _ = response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
        catch (HttpRequestException)
        {
        }
    }

    private async Task<JsonNode?> GetNodeAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(path, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw BuildException(path, response, text);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new SyncthingException("同步引擎返回了无法解析的数据。", ex);
        }
    }

    private async Task<JsonObject> GetObjectAsync(string path, CancellationToken cancellationToken)
    {
        var node = await GetNodeAsync(path, cancellationToken).ConfigureAwait(false);
        return node as JsonObject
            ?? throw new SyncthingException("同步引擎返回了非预期的数据格式。");
    }

    private async Task PutAsync(string path, JsonObject body, CancellationToken cancellationToken)
    {
        await SendNoContentAsync(HttpMethod.Put, path, body, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendNoContentAsync(
        HttpMethod method, string path, JsonObject? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw BuildException(path, response, text);
        }
    }

    private static SyncthingException BuildException(string path, HttpResponseMessage response, string body)
    {
        var detail = body.Trim();
        if (detail.Length > 700)
        {
            detail = detail[..700] + "…";
        }

        var message = string.IsNullOrWhiteSpace(detail)
            ? $"同步引擎请求失败（HTTP {(int)response.StatusCode}）。"
            : $"同步引擎请求失败（HTTP {(int)response.StatusCode}）：{detail}";
        return new SyncthingException(message);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _http.Dispose();
    }
}
