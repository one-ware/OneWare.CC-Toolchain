namespace OneWare.CologneChip.Services;

using Avalonia.Media;
using Avalonia.Threading;
using OneWare.CologneChip.Helpers;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using Prism.Ioc;

public abstract class CcCompileStrategyBase : ICologneChipCompileStrategy
{
    protected readonly IDockService Dock;
    protected readonly IChildProcessService Proc;
    protected readonly ILogger Log;
    protected readonly IOutputService Out;
    protected readonly ISettingsService Settings;

    protected CcCompileStrategyBase()
    {
        Dock     = ContainerLocator.Container.Resolve<IDockService>();
        Proc     = ContainerLocator.Container.Resolve<IChildProcessService>();
        Log      = ContainerLocator.Container.Resolve<ILogger>();
        Out      = ContainerLocator.Container.Resolve<IOutputService>();
        Settings = ContainerLocator.Container.Resolve<ISettingsService>();
    }

    // === Template: Synth ===
    public async Task<bool> SynthAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        try
        {
            var properties = FpgaSettingsParser.LoadSettings(project, fpgaModel.Fpga.Name);
            var topHeader  = project.TopEntity?.Header ?? throw new Exception("TopEntity not set!");
            var (topName, topLang) = SplitTop(topHeader);

            var buildDir = Path.Combine(project.FullPath, "build");
            Directory.CreateDirectory(buildDir);
            Dock.Show<IOutputService>();

            var start = DateTime.Now;

            // files & flags
            var included = project.Files
                .Where(f => GetIncludedExtensions(topLang).Contains(f.Extension))
                .Where(f => !project.CompileExcluded.Contains(f))
                .Where(f => !project.TestBenches.Contains(f))
                .Select(f => $"./../{f.RelativePath}");

            var yosysFlags = (properties.GetValueOrDefault("yosysToolchainYosysFlags") ?? string.Empty)
                .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var yosysSynthTool = properties.GetValueOrDefault("yosysToolchainYosysSynthTool")
                                 ?? throw new Exception("Yosys Tool not set!");

            var yosysArgs = BuildYosysArgs(topName, topLang, topHeader, yosysSynthTool)
                            .Concat(yosysFlags)
                            .Concat(included)
                            .ToList();

            var yosysPath = ResolveYosysPath(project);

            var (success, _) = await ExecWithOutput(
                yosysPath,
                yosysArgs,
                $"{project.FullPath}/build",
                "Running yosys...");

            success = MaybeIgnoreSynthExitCode(success);

            WritePhaseResult("Synthesis", start, success);
            return success;
        }
        catch (Exception e)
        {
            Log.Error(e.Message, e);
            return false;
        }
    }

    // === Template: P&R ===
    public async Task<bool> PrAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        var start = DateTime.Now;
        var topHeader  = project.TopEntity?.Header ?? throw new Exception("TopEntity not set!");
        var (topName, topLang) = SplitTop(topHeader);

        var ccfFile = CologneChipSettingsHelper.GetConstraintFile(project);
        var (exe, args) = BuildPrCommand(topName, topLang, ccfFile);
        
        var success = (await Proc.ExecuteShellAsync(exe, args,
            $"{project.FullPath}/build", $"Running P_R...", AppState.Loading, true, null, s =>
            {
                Dispatcher.UIThread.Post(() => { Out.WriteLine(s); });
                return true;
            })).success;
        
        WritePhaseResult("Place and Route", start, success);
        return success;
    }

    // === Template: Pack ===
    public virtual async Task<bool> PackAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        // default: no-op/true
        return await Task.FromResult(true);
    }

    // ---------- Hooks to override ----------

    protected abstract IEnumerable<string> BuildYosysArgs(
        string topName, string topLang, string topHeader, string yosysSynthTool);

    protected abstract (string exe, List<string> args) BuildPrCommand(
        string topName, string topLang, string ccfFile);

    protected virtual IEnumerable<string> GetIncludedExtensions(string topLang) =>
        topLang == "v" ? new[] { ".v", ".sv" } : Array.Empty<string>();

    protected virtual string ResolveYosysPath(UniversalFpgaProjectRoot project)
    {
        var src = ContainerLocator.Container.Resolve<CcSettingsService>()
                     .GetSetting(CologneChipConstantService.YosysSourceSettingsKey, project);

        if (src == "CologneChip")
        {
            var path = Settings.GetSettingValue<string>(CologneChipConstantService.CcPathSetting);
            var p = $"{path}/bin/yosys/yosys";
            Log.Log($"Yosys exec path: {p}");
            return p;
        }
        return "yosys";
    }

    // ---------- Utilities ----------

    protected static (string name, string lang) SplitTop(string header)
    {
        var parts = header.Split('.');
        return (parts.First(), parts.Last());
    }

    protected bool MaybeIgnoreSynthExitCode(bool success)
    {
        if (success) return true;

        var ignore = Settings.GetSettingValue<bool>(CologneChipConstantService.CologneChipSettingsIgnoreSynthExitCode);
        if (ignore)
        {
            Log.Warning("The synthesis was terminated with a non-zero exit code.");
            Log.Warning($"Setting '{CologneChipConstantService.CologneChipSettingsIgnoreSynthExitCode}' is true; continuing.");
            return true;
        }
        return false;
    }

    protected async Task<(bool success, string output)> ExecWithOutput(
        string exe,
        IReadOnlyCollection<string> args,
        string cwd,
        string title)
    {
        return await Proc.ExecuteShellAsync(exe, args, cwd, title, AppState.Loading, true, x =>
        {
            if (x.StartsWith("Error:"))
            {
                Log.Error(x);
                return false;
            }

            Out.WriteLine(x);
            return true;
        });
    }

    protected void WritePhaseResult(string phase, DateTime start, bool success)
    {
        var t = DateTime.Now - start;
        var msg = $"==================\n\n{phase} {(success ? "finished" : "failed")} after {(int)t.TotalMinutes:D2}:{t.Seconds:D2}\n";
        if (success) Out.WriteLine(msg);
        else Out.WriteLine(msg, Brushes.Red);
    }
}