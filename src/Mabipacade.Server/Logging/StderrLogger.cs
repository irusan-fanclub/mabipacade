namespace Mabipacade.Server.Logging;

internal static class StderrLogger
{
    public static void Info(string msg) => Console.Error.WriteLine($"[info] {msg}");
    public static void Warn(string msg) => Console.Error.WriteLine($"[warn] {msg}");
    public static void Error(string msg) => Console.Error.WriteLine($"[error] {msg}");
}
