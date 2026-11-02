using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using CcDock.App;

namespace CcDock.Syncthing;

public sealed class SyncthingService : IAsyncDisposable
{
    private readonly string _home;
    private readonly string _exePath;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly SemaphoreSlim _configGate = new(1, 1);

    private Process? _process;
    private ProcessJob? _job;
    private SyncthingClient? _client;
    private bool _stopping;
    private bool _disposed;
    private EngineState _state = EngineState.Stopped;
    private string? _lastError;
    private string _deviceId = string.Empty;
    private string _deviceName = string.Empty;
    private string _version = string.Empty;
    private string _guiAddress = string.Empty;

    public SyncthingService(string? home = null, string? exePath = null)
    {
        _home = home ?? AppPaths.SyncthingHome;
        _exePath = exePath ?? AppPaths.SyncthingExe;
    }

    public event EventHandler<EngineStateChangedEventArgs>? StateChanged;

    public EngineState State => _state;

    public bool IsRunning => _state == EngineState.Running && _client is not null;

    public string? LastError => _lastError;

    public string DeviceId => _deviceId;

    public string DeviceName => _deviceName;

    public string Version => _version;

    public string GuiAddress => _guiAddress;

    public string DataDirectory => _home;

    public SyncthingClient Client => _client
        ?? throw new InvalidOperationException("同步引擎尚未就绪。");

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return false;
            }

            if (IsRunning)
            {
                return true;
            }

            _lastError = null;
            SetState(EngineState.Starting);

            if (!File.Exists(_exePath))
            {
                SetError("未找到同步引擎文件（syncthing\\syncthing.exe）。请重新安装 ccDock。");
                return false;
            }

            Directory.CreateDirectory(_home);
            var configPath = Path.Combine(_home, "config.xml");
            if (!File.Exists(configPath))
            {
                AppLog.Info("Generating a new Syncthing configuration.");
                var generated = await RunProcessAsync(
                        new[] { "generate", "--home=" + _home },
                        TimeSpan.FromSeconds(60),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (generated.ExitCode != 0)
                {
                    AppLog.Error("Syncthing generate failed: " + generated.Output);
                    SetError("无法初始化同步引擎。请查看日志以获取详细信息。");
                    return false;
                }
            }

            var gui = ReadGuiSettings(configPath);
            _guiAddress = gui.Address;
            _client?.Dispose();
            _client = new SyncthingClient(gui.Address, gui.ApiKey);

            var alreadyRunning = await _client.PingAsync(cancellationToken).ConfigureAwait(false);
            if (!alreadyRunning)
            {
                _stopping = false;
                if (!LaunchEngine())
                {
                    return false;
                }
            }
            else
            {
                AppLog.Info("Adopted an already running Syncthing process.");
            }

            if (!await WaitForApiAsync(cancellationToken).ConfigureAwait(false))
            {
                SetError(LastStartupFailure() ?? "同步引擎启动超时。请查看日志。");
                await StopProcessAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            var status = await _client.GetSystemStatusAsync(cancellationToken).ConfigureAwait(false);
            _deviceId = JsonUtil.GetString(status, "myID");
            _version = await _client.GetVersionAsync(cancellationToken).ConfigureAwait(false);

            var config = await _client.GetConfigAsync(cancellationToken).ConfigureAwait(false);
            _deviceName = ReadLocalDeviceName(config, _deviceId);
            _state = EngineState.Running;
            _lastError = null;
            AppLog.Info($"Sync engine ready. device={_deviceId} version={_version} gui={_guiAddress}");
            RaiseStateChanged();
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Unable to start the sync engine.", ex);
            SetError(FormatStartupError(ex));
            await StopProcessAsync(CancellationToken.None).ConfigureAwait(false);
            return false;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopProcessAsync(cancellationToken).ConfigureAwait(false);
            SetState(EngineState.Stopped);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task<bool> RestartAsync(CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken).ConfigureAwait(false);
        return await StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FolderView>> GetFolderViewsAsync(CancellationToken cancellationToken = default)
    {
        var client = Client;
        var config = await client.GetConfigAsync(cancellationToken).ConfigureAwait(false);
        var connections = await client.GetConnectionsAsync(cancellationToken).ConfigureAwait(false);
        var connectionMap = JsonUtil.GetObject(connections, "connections");
        var deviceMap = ReadDevices(config);
        var selfId = _deviceId;

        var folders = ReadFolders(config);
        var views = new List<FolderView>(folders.Count);
        foreach (var folder in folders)
        {
            FolderStatusInfo status;
            try
            {
                var raw = await client.GetFolderStatusAsync(folder.Id, cancellationToken).ConfigureAwait(false);
                status = ReadFolderStatus(raw);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AppLog.Warn($"Unable to read status for folder {folder.Id}: {ex.Message}");
                status = new FolderStatusInfo();
            }

            var remotes = folder.DeviceIds
                .Where(id => !string.Equals(id, selfId, StringComparison.OrdinalIgnoreCase))
                .Select(id => deviceMap.TryGetValue(id, out var device)
                    ? device
                    : new DeviceInfo(id, "对方的电脑", false, new[] { "dynamic" }))
                .ToList();

            var connected = remotes.Any(device =>
                connectionMap?[device.DeviceId] is JsonObject connection &&
                JsonUtil.GetBool(connection, "connected"));

            double? completion = null;
            if (remotes.Count > 0)
            {
                try
                {
                    var raw = await client.GetCompletionAsync(folder.Id, remotes[0].DeviceId, cancellationToken)
                        .ConfigureAwait(false);
                    completion = JsonUtil.GetLong(raw, "completion", 100) / 100.0;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    completion = null;
                }
            }

            views.Add(new FolderView(folder, status, remotes, connected, completion));
        }

        return views;
    }

    public async Task<IReadOnlyList<DeviceView>> GetDeviceViewsAsync(
        CancellationToken cancellationToken = default)
    {
        var client = Client;
        var config = await client.GetConfigAsync(cancellationToken).ConfigureAwait(false);
        var connections = await client.GetConnectionsAsync(cancellationToken).ConfigureAwait(false);
        var connectionMap = JsonUtil.GetObject(connections, "connections");
        var deviceMap = ReadDevices(config);

        var views = new List<DeviceView>(deviceMap.Count);
        foreach (var device in deviceMap.Values)
        {
            var isSelf = string.Equals(device.DeviceId, _deviceId, StringComparison.OrdinalIgnoreCase);
            var connection = connectionMap?[device.DeviceId] as JsonObject;
            var connected = isSelf || (connection is not null && JsonUtil.GetBool(connection, "connected"));
            var address = connection is null ? string.Empty : JsonUtil.GetString(connection, "address");
            var connectionType = connection is null ? string.Empty : JsonUtil.GetString(connection, "type");
            var viaRelay = connectionType.StartsWith("relay", StringComparison.OrdinalIgnoreCase)
                || (connection is not null && JsonUtil.GetBool(connection, "relay"));
            views.Add(new DeviceView(device, isSelf, connected, address, connectionType, viaRelay));
        }

        return views
            .OrderBy(view => view.IsSelf ? 0 : 1)
            .ThenBy(view => view.Device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<ConnectionSettings> GetConnectionSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var options = await Client.GetOptionsAsync(cancellationToken).ConfigureAwait(false);
        return new ConnectionSettings(
            JsonUtil.GetBool(options, "globalAnnounceEnabled", true),
            JsonUtil.GetBool(options, "localAnnounceEnabled", true),
            JsonUtil.GetBool(options, "relaysEnabled", true),
            JsonUtil.GetBool(options, "natEnabled", true),
            ReadStringArray(options, "listenAddresses"));
    }

    public async Task SaveConnectionSettingsAsync(
        ConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        await _configGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var patch = new JsonObject
            {
                ["globalAnnounceEnabled"] = settings.GlobalDiscovery,
                ["localAnnounceEnabled"] = settings.LocalDiscovery,
                ["relaysEnabled"] = settings.Relays,
                ["natEnabled"] = settings.NatEnabled
            };
            await Client.PatchOptionsAsync(patch, cancellationToken).ConfigureAwait(false);
            AppLog.Info(
                $"Connection settings saved. global={settings.GlobalDiscovery} " +
                $"local={settings.LocalDiscovery} relays={settings.Relays} nat={settings.NatEnabled}");
        }
        finally
        {
            _configGate.Release();
        }
    }

    public async Task SetDeviceAddressesAsync(
        string deviceId,
        IEnumerable<string> addresses,
        CancellationToken cancellationToken = default)
    {
        if (!DeviceIdValidator.TryNormalize(deviceId, out var normalized, out var error))
        {
            throw new SyncthingException(error ?? "设备 ID 无效。");
        }

        if (string.Equals(normalized, _deviceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new SyncthingException("本机地址由监听设置决定，不能在这里修改。");
        }

        var cleaned = addresses
            .Select(address => address.Trim())
            .Where(address => address.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleaned.Count == 0)
        {
            cleaned.Add("dynamic");
        }

        foreach (var address in cleaned)
        {
            if (!IsValidDeviceAddress(address))
            {
                throw new SyncthingException(
                    $"地址“{address}”格式无效。示例：dynamic、tcp://192.168.1.50:22000、quic://192.168.1.50:22000");
            }
        }

        await _configGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var client = Client;
            var config = await client.GetConfigAsync(cancellationToken).ConfigureAwait(false);
            if (JsonUtil.GetArray(config, "devices") is not { } devices)
            {
                throw new SyncthingException("无法读取设备配置。");
            }

            foreach (var item in devices)
            {
                if (item is not JsonObject device ||
                    !string.Equals(
                        JsonUtil.GetString(device, "deviceID"),
                        normalized,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var array = new JsonArray();
                foreach (var address in cleaned)
                {
                    array.Add(address);
                }
                device["addresses"] = array;
                await client.PutDeviceAsync(normalized, device, cancellationToken).ConfigureAwait(false);
                AppLog.Info(
                    $"Device addresses updated. device={normalized} addresses={string.Join(",", cleaned)}");
                return;
            }

            throw new SyncthingException("设备不存在，请先添加该设备。");
        }
        finally
        {
            _configGate.Release();
        }
    }

    public async Task<(IReadOnlyList<PendingDeviceInfo> Devices, IReadOnlyList<PendingFolderOffer> Folders)>
        GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var client = Client;
        var config = await client.GetConfigAsync(cancellationToken).ConfigureAwait(false);
        var deviceMap = ReadDevices(config);
        var pendingDevicesRaw = await client.GetPendingDevicesAsync(cancellationToken).ConfigureAwait(false);
        var pendingFoldersRaw = await client.GetPendingFoldersAsync(cancellationToken).ConfigureAwait(false);

        var devices = new List<PendingDeviceInfo>();
        foreach (var pair in pendingDevicesRaw)
        {
            if (pair.Value is not JsonObject value)
            {
                continue;
            }

            devices.Add(new PendingDeviceInfo(
                pair.Key,
                JsonUtil.GetString(value, "name", DeviceIdValidator.Shorten(pair.Key)),
                JsonUtil.GetString(value, "address"),
                ParseTime(JsonUtil.GetString(value, "time"))));
        }

        var folders = new List<PendingFolderOffer>();
        foreach (var pair in pendingFoldersRaw)
        {
            if (pair.Value is not JsonObject folder || JsonUtil.GetObject(folder, "offeredBy") is not { } offeredBy)
            {
                continue;
            }

            foreach (var offerPair in offeredBy)
            {
                if (offerPair.Value is not JsonObject offer)
                {
                    continue;
                }

                var deviceId = offerPair.Key;
                var deviceName = deviceMap.TryGetValue(deviceId, out var device)
                    ? device.Name
                    : DeviceIdValidator.Shorten(deviceId);
                folders.Add(new PendingFolderOffer(
                    pair.Key,
                    JsonUtil.GetString(offer, "label", pair.Key),
                    deviceId,
                    deviceName,
                    JsonUtil.GetBool(offer, "receiveEncrypted")));
            }
        }

        return (devices, folders);
    }

    public async Task AcceptPendingDeviceAsync(
        string deviceId, string? fallbackName = null, CancellationToken cancellationToken = default)
    {
        if (!DeviceIdValidator.TryNormalize(deviceId, out var normalized, out var error))
        {
            throw new SyncthingException(error ?? "设备 ID 无效。");
        }

        if (string.Equals(normalized, _deviceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new SyncthingException("这是本机的设备 ID，请输入另一台电脑的 ID。");
        }

        await _configGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureDeviceAsync(normalized, fallbackName, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _configGate.Release();
        }
    }

    public async Task<string> CreateBackupTaskAsync(
        string localPath,
        string remoteDeviceId,
        string label,
        CancellationToken cancellationToken = default)
    {
        if (!DeviceIdValidator.TryNormalize(remoteDeviceId, out var remoteId, out var error))
        {
            throw new SyncthingException(error ?? "设备 ID 无效。");
        }

        if (string.Equals(remoteId, _deviceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new SyncthingException("接收方不能是本机，请输入另一台电脑的设备 ID。");
        }

        var fullPath = NormalizePath(localPath);
        if (!Directory.Exists(fullPath))
        {
            throw new SyncthingException("选择的文件夹不存在。");
        }

        await _configGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var client = Client;
            var config = await client.GetConfigAsync(cancellationToken).ConfigureAwait(false);
            var existingFolders = ReadFolders(config);
            EnsureNoPathOverlap(fullPath, existingFolders, null);
            await EnsureDeviceAsync(remoteId, "对方的电脑", cancellationToken).ConfigureAwait(false);

            string folderId;
            do
            {
                folderId = "ccdock-" + Guid.NewGuid().ToString("N")[..12];
            } while (existingFolders.Any(f => f.Id == folderId));

            var folder = await client.GetFolderDefaultsAsync(cancellationToken).ConfigureAwait(false);
            folder["id"] = folderId;
            folder["label"] = string.IsNullOrWhiteSpace(label) ? Path.GetFileName(fullPath) : label.Trim();
            folder["path"] = fullPath;
            folder["type"] = "sendonly";
            folder["paused"] = false;
            folder["fsWatcherEnabled"] = true;
            folder["fsWatcherDelayS"] = 5;
            folder["rescanIntervalS"] = 3600;
            folder["devices"] = new JsonArray(
                new JsonObject { ["deviceID"] = _deviceId },
                new JsonObject { ["deviceID"] = remoteId });

            await client.PutFolderAsync(folderId, folder, cancellationToken).ConfigureAwait(false);
            AppLog.Info($"Backup task created. folder={folderId} path={fullPath} remote={remoteId}");
            return folderId;
        }
        finally
        {
            _configGate.Release();
        }
    }

    public async Task AcceptPendingFolderAsync(
        string folderId,
        string label,
        string sourceDeviceId,
        string localPath,
        bool keepVersions,
        CancellationToken cancellationToken = default)
    {
        if (!DeviceIdValidator.TryNormalize(sourceDeviceId, out var sourceId, out var error))
        {
            throw new SyncthingException(error ?? "来源设备 ID 无效。");
        }

        if (string.IsNullOrWhiteSpace(folderId))
        {
            throw new SyncthingException("共享文件夹标识无效。");
        }

        var fullPath = NormalizePath(localPath);
        if (string.IsNullOrWhiteSpace(fullPath) || Path.GetPathRoot(fullPath) == fullPath)
        {
            throw new SyncthingException("不能将整个磁盘作为备份保存位置，请选择子文件夹。");
        }

        Directory.CreateDirectory(fullPath);

        await _configGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var client = Client;
            var config = await client.GetConfigAsync(cancellationToken).ConfigureAwait(false);
            var existingFolders = ReadFolders(config);
            if (existingFolders.Any(f => string.Equals(f.Id, folderId, StringComparison.Ordinal)))
            {
                throw new SyncthingException("这个备份任务已经接收过了。");
            }

            EnsureNoPathOverlap(fullPath, existingFolders, null);
            await EnsureDeviceAsync(sourceId, null, cancellationToken).ConfigureAwait(false);

            var folder = await client.GetFolderDefaultsAsync(cancellationToken).ConfigureAwait(false);
            folder["id"] = folderId;
            folder["label"] = string.IsNullOrWhiteSpace(label) ? folderId : label.Trim();
            folder["path"] = fullPath;
            folder["type"] = "receiveonly";
            folder["paused"] = false;
            folder["fsWatcherEnabled"] = true;
            folder["fsWatcherDelayS"] = 5;
            folder["rescanIntervalS"] = 3600;
            folder["devices"] = new JsonArray(
                new JsonObject { ["deviceID"] = _deviceId },
                new JsonObject { ["deviceID"] = sourceId });

            if (keepVersions)
            {
                folder["versioning"] = new JsonObject
                {
                    ["type"] = "staggered",
                    ["params"] = new JsonObject
                    {
                        ["maxAge"] = "2592000",
                        ["cleanInterval"] = "3600",
                        ["versionsPath"] = ""
                    },
                    ["cleanupIntervalS"] = 3600,
                    ["fsPath"] = "",
                    ["fsType"] = "basic"
                };
            }

            await client.PutFolderAsync(folderId, folder, cancellationToken).ConfigureAwait(false);
            AppLog.Info($"Folder offer accepted. folder={folderId} path={fullPath} source={sourceId}");
        }
        finally
        {
            _configGate.Release();
        }
    }

    public async Task IgnorePendingDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        await Client.IgnorePendingDeviceAsync(deviceId, cancellationToken).ConfigureAwait(false);
        AppLog.Info($"Pending device request ignored: {DeviceIdValidator.Shorten(deviceId)}");
    }

    public async Task RemoveFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        await _configGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Client.DeleteFolderAsync(folderId, cancellationToken).ConfigureAwait(false);
            AppLog.Info($"Folder removed from ccDock: {folderId}");
        }
        finally
        {
            _configGate.Release();
        }
    }

    public async Task RenameLocalDeviceAsync(string newName, CancellationToken cancellationToken = default)
    {
        var name = newName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new SyncthingException("设备名称不能为空。");
        }
        if (name.Length > 64)
        {
            throw new SyncthingException("设备名称不能超过 64 个字符。");
        }

        await _configGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var client = Client;
            var config = await client.GetConfigAsync(cancellationToken).ConfigureAwait(false);
            if (JsonUtil.GetArray(config, "devices") is not { } devices)
            {
                throw new SyncthingException("无法读取本机设备配置。");
            }

            foreach (var item in devices)
            {
                if (item is JsonObject device &&
                    string.Equals(JsonUtil.GetString(device, "deviceID"), _deviceId, StringComparison.OrdinalIgnoreCase))
                {
                    device["name"] = name;
                    await client.PutDeviceAsync(_deviceId, device, cancellationToken).ConfigureAwait(false);
                    _deviceName = name;
                    return;
                }
            }

            throw new SyncthingException("无法找到本机设备配置。");
        }
        finally
        {
            _configGate.Release();
        }
    }

    public async Task RefreshSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var client = Client;
        var status = await client.GetSystemStatusAsync(cancellationToken).ConfigureAwait(false);
        _deviceId = JsonUtil.GetString(status, "myID", _deviceId);
        _version = await client.GetVersionAsync(cancellationToken).ConfigureAwait(false);
        var config = await client.GetConfigAsync(cancellationToken).ConfigureAwait(false);
        _deviceName = ReadLocalDeviceName(config, _deviceId);
    }

    private async Task EnsureDeviceAsync(string deviceId, string? fallbackName, CancellationToken cancellationToken)
    {
        var client = Client;
        var config = await client.GetConfigAsync(cancellationToken).ConfigureAwait(false);
        if (JsonUtil.GetArray(config, "devices") is { } devices)
        {
            foreach (var item in devices)
            {
                if (item is JsonObject device &&
                    string.Equals(JsonUtil.GetString(device, "deviceID"), deviceId, StringComparison.OrdinalIgnoreCase))
                {
                    if (JsonUtil.GetBool(device, "paused"))
                    {
                        device["paused"] = false;
                        await client.PutDeviceAsync(deviceId, device, cancellationToken).ConfigureAwait(false);
                    }
                    return;
                }
            }
        }

        var name = string.IsNullOrWhiteSpace(fallbackName)
            ? "对方的电脑（" + DeviceIdValidator.Shorten(deviceId) + "）"
            : fallbackName.Trim();

        var newDevice = await client.GetDeviceDefaultsAsync(cancellationToken).ConfigureAwait(false);
        newDevice["deviceID"] = deviceId;
        newDevice["name"] = name;
        newDevice["addresses"] = new JsonArray("dynamic");
        newDevice["paused"] = false;
        newDevice["autoAcceptFolders"] = false;
        await client.PutDeviceAsync(deviceId, newDevice, cancellationToken).ConfigureAwait(false);
    }

    private bool LaunchEngine()
    {
        _job ??= new ProcessJob();
        var startInfo = new ProcessStartInfo
        {
            FileName = _exePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(_exePath) ?? AppPaths.InstallDirectory
        };
        startInfo.ArgumentList.Add("serve");
        startInfo.ArgumentList.Add("--home=" + _home);
        startInfo.ArgumentList.Add("--no-browser");
        startInfo.ArgumentList.Add("--no-restart");
        startInfo.ArgumentList.Add("--no-upgrade");
        startInfo.ArgumentList.Add("--no-console");

        try
        {
            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            process.Exited += ProcessOnExited;
            if (!process.Start())
            {
                throw new SyncthingException("无法启动同步引擎进程。");
            }

            _process = process;
            _job.TryAssign(process.Handle);
            AppLog.Info($"Started Syncthing process (PID {process.Id}).");
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Unable to launch Syncthing.", ex);
            _lastError = "无法启动同步引擎：" + ex.Message;
            return false;
        }
    }

    private async Task<bool> WaitForApiAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(75);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_process is { HasExited: true })
            {
                var tail = ReadSyncthingLogTail();
                _lastError = "同步引擎在启动时退出。" +
                    (string.IsNullOrWhiteSpace(tail) ? string.Empty : "\n\n" + tail);
                AppLog.Error("Syncthing exited during startup. " + tail);
                return false;
            }

            if (_client is not null && await _client.PingAsync(cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(450, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private async Task StopProcessAsync(CancellationToken cancellationToken)
    {
        _stopping = true;
        var client = _client;
        var process = _process;

        if (client is not null)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(4));
                await client.ShutdownAsync(cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLog.Warn("Graceful Syncthing shutdown failed: " + ex.Message);
            }
        }

        if (process is not null)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Warn("Unable to terminate Syncthing: " + ex.Message);
                }
            }

            try
            {
                process.Exited -= ProcessOnExited;
                process.Dispose();
            }
            catch
            {
                // ignore
            }
            _process = null;
        }

        client?.Dispose();
        _client = null;
        _job?.Dispose();
        _job = null;
        _stopping = false;

        if (_state != EngineState.Error || _lastError is null)
        {
            _lastError = null;
        }
    }

    private void ProcessOnExited(object? sender, EventArgs e)
    {
        if (_stopping || _disposed)
        {
            return;
        }

        AppLog.Warn("Syncthing process exited unexpectedly.");
        _lastError = "同步引擎意外退出。请在设置中重试启动。";
        RaiseStateChanged(EngineState.Error, _lastError);
    }

    private void SetError(string message)
    {
        _lastError = message;
        AppLog.Error(message);
        RaiseStateChanged(EngineState.Error, message);
    }

    private string FormatStartupError(Exception exception)
    {
        var message = exception is SyncthingException
            ? exception.Message
            : "无法启动同步引擎：" + exception.Message;
        return message + "\n\n日志位置：" + AppPaths.LogsDirectory;
    }

    private string? LastStartupFailure() => _lastError;

    private void SetState(EngineState state)
    {
        if (_state == state)
        {
            return;
        }
        _state = state;
        RaiseStateChanged(state, state == EngineState.Error ? _lastError : null);
    }

    private void RaiseStateChanged(EngineState? state = null, string? message = null)
    {
        var actual = state ?? _state;
        _state = actual;
        StateChanged?.Invoke(this, new EngineStateChangedEventArgs(actual, message));
    }

    private string ReadSyncthingLogTail()
    {
        try
        {
            var logPath = Path.Combine(_home, "syncthing.log");
            if (!File.Exists(logPath))
            {
                return string.Empty;
            }

            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var lines = new List<string>();
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
                if (lines.Count > 12)
                {
                    lines.RemoveAt(0);
                }
            }
            return string.Join(Environment.NewLine, lines);
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<(int ExitCode, string Output)> RunProcessAsync(
        IEnumerable<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        // The exe is supplied by the caller (Syncthing). Arguments are passed
        // through ArgumentList, never through a command shell.
        var startInfo = new ProcessStartInfo
        {
            FileName = _exePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignore
            }
            throw;
        }

        var output = (await stdout.ConfigureAwait(false)) + (await stderr.ConfigureAwait(false));
        return (process.ExitCode, output);
    }

    private static GuiSettings ReadGuiSettings(string configPath)
    {
        using var reader = XmlReader.Create(configPath, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        var document = XDocument.Load(reader);
        var gui = document.Root?.Element("gui")
            ?? throw new SyncthingException("同步引擎配置文件无效。");
        var address = gui.Element("address")?.Value?.Trim();
        var apiKey = gui.Element("apikey")?.Value?.Trim();
        var tls = string.Equals(gui.Attribute("tls")?.Value, "true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new SyncthingException("同步引擎未配置管理地址。");
        }
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new SyncthingException("同步引擎未配置 API 密钥。");
        }

        address = address
            .Replace("0.0.0.0", "127.0.0.1", StringComparison.Ordinal)
            .Replace("[::]", "[::1]", StringComparison.Ordinal);
        if (!address.Contains("://", StringComparison.Ordinal))
        {
            address = (tls ? "https://" : "http://") + address;
        }

        return new GuiSettings(address, apiKey);
    }

    private static string ReadLocalDeviceName(JsonObject config, string deviceId)
    {
        if (JsonUtil.GetArray(config, "devices") is { } devices)
        {
            foreach (var item in devices)
            {
                if (item is JsonObject device &&
                    string.Equals(JsonUtil.GetString(device, "deviceID"), deviceId, StringComparison.OrdinalIgnoreCase))
                {
                    var name = JsonUtil.GetString(device, "name");
                    return string.IsNullOrWhiteSpace(name) ? Environment.MachineName : name;
                }
            }
        }
        return Environment.MachineName;
    }

    private static List<FolderInfo> ReadFolders(JsonObject config)
    {
        var result = new List<FolderInfo>();
        if (JsonUtil.GetArray(config, "folders") is not { } folders)
        {
            return result;
        }

        foreach (var item in folders)
        {
            if (item is not JsonObject folder)
            {
                continue;
            }

            var ids = new List<string>();
            if (JsonUtil.GetArray(folder, "devices") is { } deviceArray)
            {
                foreach (var deviceItem in deviceArray)
                {
                    if (deviceItem is JsonObject device)
                    {
                        var id = JsonUtil.GetString(device, "deviceID");
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            ids.Add(id);
                        }
                    }
                }
            }

            result.Add(new FolderInfo(
                JsonUtil.GetString(folder, "id"),
                JsonUtil.GetString(folder, "label"),
                JsonUtil.GetString(folder, "path"),
                JsonUtil.GetString(folder, "type"),
                JsonUtil.GetBool(folder, "paused"),
                ids));
        }

        return result;
    }

    private static Dictionary<string, DeviceInfo> ReadDevices(JsonObject config)
    {
        var result = new Dictionary<string, DeviceInfo>(StringComparer.OrdinalIgnoreCase);
        if (JsonUtil.GetArray(config, "devices") is not { } devices)
        {
            return result;
        }

        foreach (var item in devices)
        {
            if (item is not JsonObject device)
            {
                continue;
            }

            var id = JsonUtil.GetString(device, "deviceID");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var addresses = new List<string>();
            if (JsonUtil.GetArray(device, "addresses") is { } addressArray)
            {
                foreach (var address in addressArray)
                {
                    try
                    {
                        addresses.Add(address?.GetValue<string>() ?? string.Empty);
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            result[id] = new DeviceInfo(
                id,
                JsonUtil.GetString(device, "name", DeviceIdValidator.Shorten(id)),
                JsonUtil.GetBool(device, "paused"),
                addresses);
        }

        return result;
    }

    private static FolderStatusInfo ReadFolderStatus(JsonObject raw)
        => new()
        {
            State = JsonUtil.GetString(raw, "state", "unknown"),
            NeedBytes = JsonUtil.GetLong(raw, "needBytes"),
            GlobalBytes = JsonUtil.GetLong(raw, "globalBytes"),
            NeedFiles = JsonUtil.GetLong(raw, "needFiles"),
            Errors = JsonUtil.GetInt(raw, "errors"),
            ErrorMessage = JsonUtil.GetString(raw, "error"),
            WatchError = JsonUtil.GetString(raw, "watchError")
        };

    private static void EnsureNoPathOverlap(
        string path, IEnumerable<FolderInfo> folders, string? exceptFolderId)
    {
        var normalized = NormalizePath(path);
        foreach (var folder in folders)
        {
            if (exceptFolderId is not null &&
                string.Equals(folder.Id, exceptFolderId, StringComparison.Ordinal))
            {
                continue;
            }

            var other = NormalizePath(folder.Path);
            if (string.IsNullOrWhiteSpace(other))
            {
                continue;
            }

            if (string.Equals(normalized, other, StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(other + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                other.StartsWith(normalized + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new SyncthingException(
                    $"该文件夹与已有任务“{folder.Label}”的路径重叠，请选择其他文件夹。");
            }
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonObject source, string property)
    {
        var result = new List<string>();
        if (JsonUtil.GetArray(source, property) is not { } array)
        {
            return result;
        }

        foreach (var item in array)
        {
            try
            {
                var value = item?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }
            catch
            {
                // Ignore malformed entries.
            }
        }

        return result;
    }

    private static bool IsValidDeviceAddress(string address)
    {
        if (string.Equals(address, "dynamic", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme.ToLowerInvariant() switch
        {
            "tcp" or "quic" =>
                !string.IsNullOrWhiteSpace(uri.Host) && uri.Port > 0 && uri.Port <= 65535,
            "relay" => true,
            _ => false
        };
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path;
        }
    }

    private static DateTimeOffset? ParseTime(string value)
        => DateTimeOffset.TryParse(value, out var time) ? time : null;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true;
        _lifecycle.Dispose();
        _configGate.Dispose();
    }

    private sealed record GuiSettings(string Address, string ApiKey);
}
