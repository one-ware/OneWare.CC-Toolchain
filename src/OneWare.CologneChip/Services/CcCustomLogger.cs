using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OneWare.Essentials.Services;
namespace OneWare.CologneChip.Services;

public class CcCustomLogger(ILogger logger) : ICcCustomLogger
{
    private const ConsoleColor LogMessageConsoleColor = ConsoleColor.Cyan;

    private static readonly IBrush LogMessageBrush =
        (Application.Current!.GetResourceObservable("ThemeAccentBrush") as IBrush)!;

    private readonly string _assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name!;

    public void Log(string message, bool showOutput = false)
    {
        logger.Log("[" + _assemblyName + "]: " + message, LogMessageConsoleColor, showOutput, LogMessageBrush);
    }

    public void Error(string message, bool showOutput = true)
    {
        logger.Error("[" + _assemblyName + "]: " + message, null, showOutput);
    }
}