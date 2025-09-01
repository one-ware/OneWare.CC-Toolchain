namespace OneWare.CologneChip.Services;

public interface ICcCustomLogger
{
    public void Log(string message, bool showOutput = false);

    public void Error(string message, bool showOutput = true);
}