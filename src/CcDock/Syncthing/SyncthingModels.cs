namespace CcDock.Syncthing;

public sealed record DeviceInfo(
    string DeviceId,
    string Name,
    bool Paused,
    IReadOnlyList<string> Addresses);

public sealed record FolderInfo(
    string Id,
    string Label,
    string Path,
    string Type,
    bool Paused,
    IReadOnlyList<string> DeviceIds);

public sealed class FolderStatusInfo
{
    public string State { get; init; } = "unknown";
    public long NeedBytes { get; init; }
    public long GlobalBytes { get; init; }
    public long NeedFiles { get; init; }
    public int Errors { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public string WatchError { get; init; } = string.Empty;
}

public sealed record FolderView(
    FolderInfo Folder,
    FolderStatusInfo Status,
    IReadOnlyList<DeviceInfo> RemoteDevices,
    bool Connected,
    double? Completion)
{
    public bool IsReceiveOnly =>
        string.Equals(Folder.Type, "receiveonly", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Folder.Type, "receiveencrypted", StringComparison.OrdinalIgnoreCase);

    public bool IsSendOnly =>
        string.Equals(Folder.Type, "sendonly", StringComparison.OrdinalIgnoreCase);
}

public sealed record DeviceView(
    DeviceInfo Device,
    bool IsSelf,
    bool Connected,
    string Address,
    string ConnectionType,
    bool ViaRelay)
{
    public bool IsDirect => Connected && !ViaRelay;
}

public sealed record ConnectionSettings(
    bool GlobalDiscovery,
    bool LocalDiscovery,
    bool Relays,
    bool NatEnabled,
    IReadOnlyList<string> ListenAddresses);

public sealed record PendingDeviceInfo(string DeviceId, string Name, string Address, DateTimeOffset? SeenAt);

public sealed record PendingFolderOffer(
    string FolderId,
    string Label,
    string DeviceId,
    string DeviceName,
    bool ReceiveEncrypted);

public sealed record EngineSnapshot(
    string DeviceId,
    string DeviceName,
    string Version,
    string GuiAddress);

public enum EngineState
{
    Stopped,
    Starting,
    Running,
    Error
}

public sealed class EngineStateChangedEventArgs : EventArgs
{
    public EngineStateChangedEventArgs(EngineState state, string? message = null)
    {
        State = state;
        Message = message;
    }

    public EngineState State { get; }
    public string? Message { get; }
}

public sealed class SyncthingException : Exception
{
    public SyncthingException(string message) : base(message)
    {
    }

    public SyncthingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
