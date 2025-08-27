using Avalonia.Media;
using Avalonia.Threading;
using OneWare.CologneChip.Helpers;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using Prism.Ioc;

namespace OneWare.CologneChip.Services;

public class CcProprietaryCompileStrategy : ICologneChipCompileStrategy
{
    private readonly IDockService _dockService;
    private readonly IChildProcessService _childProcessService;
    private readonly ILogger _logger;
    private readonly IOutputService _outputService;
    private readonly ISettingsService _settingsService;
    
    public CcProprietaryCompileStrategy()
    {
        _dockService = ContainerLocator.Container.Resolve<IDockService>();
        _childProcessService = ContainerLocator.Container.Resolve<IChildProcessService>();
        _logger = ContainerLocator.Container.Resolve<ILogger>();
        _outputService = ContainerLocator.Container.Resolve<IOutputService>();
        _settingsService = ContainerLocator.Container.Resolve<ISettingsService>();
    }
    
    public async Task<bool> SynthAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        try
        {
            var properties = FpgaSettingsParser.LoadSettings(project, fpgaModel.Fpga.Name);
            var top = project.TopEntity?.Header ?? throw new Exception("TopEntity not set!");
            
            var buildDir = Path.Combine(project.FullPath, "build");
            Directory.CreateDirectory(buildDir);

            _dockService.Show<IOutputService>();
            
            var start = DateTime.Now;

            var yosysSynthTool = properties.GetValueOrDefault("yosysToolchainYosysSynthTool") ??
                                 throw new Exception("Yosys Tool not set!");
            
            var (topName, topLanguage) = (top.Split('.').First(), top.Split('.').Last());

            List<string> yosysArguments = [];
            List<string> includedExtensions = [];
            
            switch (topLanguage)
            {
                case "vhd":
                    _outputService.WriteLine("VHDL Synthesis...\n===============");
                    yosysArguments = ["-q","-l ./synth.log",  "-p", $"ghdl --warn-no-binding -C --ieee=synopsys ./../{top} -e {topName}; {yosysSynthTool} -nomx8 -top {topName} -vlog {topName}_synth.v"];
                    includedExtensions = [];
                    break;
                case "v": 
                    _outputService.WriteLine("Verilog Synthesis...\n==============");
                    yosysArguments = ["-ql", "./synth.log", "-p", $"{yosysSynthTool} -nomx8 -top {topName} -vlog {topName}_synth.v"];
                    includedExtensions = [".v", ".sv"];
                    break;
            }
            
            var includedFiles = project.Files
                .Where(x => includedExtensions.Contains(x.Extension))
                .Where(x => !project.CompileExcluded.Contains(x))
                .Where(x => !project.TestBenches.Contains(x))
                .Select(x => $"./../{x.RelativePath}");
            
            yosysArguments.AddRange(properties.GetValueOrDefault("yosysToolchainYosysFlags")?.Split(' ',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? []);
            yosysArguments.AddRange(includedFiles);
            
            var execPath = "";

            switch (_settingsService.GetSettingValue<string>(CologneChipConstantService.OpenFPGALoaderSourceSettingsKey))
            {
                case "CologneChip":
                    var path = _settingsService.GetSettingValue<string>(CologneChipConstantService.CcPathSetting);
                    execPath = $"{path}/bin/yosys/yosys";
                    break;
                default:
                    execPath = "yosys";
                    break;
            }
            
            var (success, _) = await _childProcessService.ExecuteShellAsync(execPath, yosysArguments, $"{project.FullPath}/build",
                "Running yosys...", AppState.Loading, true, x =>
                {
                    if (x.StartsWith("Error:"))
                    {
                        _logger.Error(x);
                        return false;
                    }

                    _outputService.WriteLine(x);
                    return true;
                });
            
            
            if (!success) {
                var ignoreSynthExitCode = ContainerLocator.Container.Resolve<ISettingsService>().GetSettingValue<bool>(CologneChipConstantService.CologneChipSettingsIgnoreSynthExitCode);
                if (ignoreSynthExitCode)
                {
                    ContainerLocator.Container.Resolve<ILogger>().Warning("The synthesis was terminated with an exit code other than zero");
                    ContainerLocator.Container.Resolve<ILogger>().Warning($"Setting '{CologneChipConstantService.CologneChipSettingsIgnoreSynthExitCode}' to true");
                    ContainerLocator.Container.Resolve<ILogger>().Warning($"Because of this setting, the route and placing tool is started anyway");
                    success = true;
                } 
            }
            
            
            var compileTime = DateTime.Now - start;
            if (success)
                _outputService.WriteLine(
                    $"==================\n\nSynthesis finished after {(int)compileTime.TotalMinutes:D2}:{compileTime.Seconds:D2}\n");
            else
                _outputService.WriteLine(
                    $"==================\n\nSynthesis failed after {(int)compileTime.TotalMinutes:D2}:{compileTime.Seconds:D2}\n",
                    Brushes.Red);

            return success;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return false;
        }
    }

    public async Task<bool> PrAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        var start = DateTime.Now;
        var top = project.TopEntity?.Header ?? throw new Exception("TopEntity not set!");
        var (topName, topLanguage) = (top.Split('.').First(), top.Split('.').Last());
        var ccfFile = CologneChipSettingsHelper.GetConstraintFile(project);
        
        List<string> prArguments = ["-i", $"{topName}_synth.v", "-o", topName, $"-ccf ./../{ccfFile} -cCP"];
        
        var success = (await _childProcessService.ExecuteShellAsync("p_r", prArguments,
            $"{project.FullPath}/build", $"Running P_R...", AppState.Loading, true, null, s =>
            {
                Dispatcher.UIThread.Post(() => { _outputService.WriteLine(s); });
                return true;
            })).success;
        
        var compileTime = DateTime.Now - start;
        if (success)
            _outputService.WriteLine(
                $"==================\n\nPlace and Route finished after {(int)compileTime.TotalMinutes:D2}:{compileTime.Seconds:D2}\n");
        else
            _outputService.WriteLine(
                $"==================\n\nPlace and Route failed after {(int)compileTime.TotalMinutes:D2}:{compileTime.Seconds:D2}\n",
                Brushes.Red);
        
        return success;
    }

    public Task<bool> PackAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        return Task.FromResult(true);
    }
}