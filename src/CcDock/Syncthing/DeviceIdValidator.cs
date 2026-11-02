namespace CcDock.Syncthing;

/// <summary>
/// Normalizes and validates Syncthing device IDs. Syncthing uses four Luhn
/// check characters (one after every 13 base32 characters) to catch typos.
/// </summary>
public static class DeviceIdValidator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static bool TryNormalize(string? input, out string deviceId, out string? error)
    {
        deviceId = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "请输入对方的设备 ID。";
            return false;
        }

        var raw = input.Trim()
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .ToUpperInvariant();

        // Syncthing accepts these common handwriting/OCR substitutions.
        raw = raw.Replace('0', 'O').Replace('1', 'I').Replace('8', 'B');

        if (raw.Length == 52)
        {
            deviceId = Chunk(raw);
            return true;
        }

        if (raw.Length != 56)
        {
            error = "设备 ID 长度不正确。完整 ID 应为 8 组、共 56 位字符（例如 XXXXXXX-XXXXXXX-…）。";
            return false;
        }

        for (var block = 0; block < 4; block++)
        {
            var offset = block * 14;
            var payload = raw.Substring(offset, 13);
            var expected = raw[offset + 13];
            if (Luhn32(payload) != expected)
            {
                error = "设备 ID 校验失败，请检查是否复制完整或包含了多余字符。";
                return false;
            }
        }

        deviceId = Chunk(raw);
        return true;
    }

    public static string Shorten(string deviceId, int prefix = 7)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return "未知设备";
        }

        return deviceId.Length <= prefix ? deviceId : deviceId[..prefix] + "…";
    }

    private static string Chunk(string raw)
    {
        var groups = new List<string>();
        for (var i = 0; i < raw.Length; i += 7)
        {
            groups.Add(raw.Substring(i, Math.Min(7, raw.Length - i)));
        }
        return string.Join("-", groups);
    }

    private static char Luhn32(string input)
    {
        var factor = 1;
        var sum = 0;
        foreach (var ch in input)
        {
            var codepoint = Alphabet.IndexOf(ch);
            if (codepoint < 0)
            {
                return '\0';
            }

            var addend = factor * codepoint;
            factor = factor == 2 ? 1 : 2;
            sum += addend / 32 + addend % 32;
        }

        var remainder = sum % 32;
        return Alphabet[(32 - remainder) % 32];
    }
}
