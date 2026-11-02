using System.Text;

namespace CcDock.App;

public static class AppLog
{
    private static readonly object Gate = new();
    private const long MaxBytes = 2 * 1024 * 1024;

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message) => Write("WARN", message, null);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            lock (Gate)
            {
                RotateIfNeeded();
                var builder = new StringBuilder();
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                    .Append(" [").Append(level).Append("] ")
                    .Append(message);
                if (exception is not null)
                {
                    builder.AppendLine();
                    builder.Append(exception);
                }
                builder.AppendLine();
                File.AppendAllText(AppPaths.AppLogFile, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never take down the application.
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            var info = new FileInfo(AppPaths.AppLogFile);
            if (!info.Exists || info.Length < MaxBytes)
            {
                return;
            }

            var backup = AppPaths.AppLogFile + ".1";
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }
            File.Move(AppPaths.AppLogFile, backup);
        }
        catch
        {
            // ignore
        }
    }
}
