using Avalonia.Media;
using Avalonia.Threading;
using OneWare.CologneChip.Helpers;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using Prism.Ioc;

namespace OneWare.CologneChip.Services;

public class CologneChipService(
    IChildProcessService childProcessService,
    ILogger logger,
    IOutputService outputService,
    IDockService dockService,
    ISettingsService settingsService)
{
    public async Task<bool> SynthAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        var toolchain = settingsService.GetSettingValue<string>(CologneChipConstantService.ToolChainSettingsKey);
        return toolchain switch
        {
            "p_r" => await ContainerLocator.Container.Resolve<CcProprietaryCompileStrategy>()
                .SynthAsync(project, fpgaModel),
            "nextpnr" => await ContainerLocator.Container.Resolve<CcNextpnrCompileStrategy>()
                .SynthAsync(project, fpgaModel),
            _ => throw new Exception($"Unknown toolchain: {toolchain}")
        };
    }

    public async Task<bool> PrAysnc(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        var toolchain = settingsService.GetSettingValue<string>(CologneChipConstantService.ToolChainSettingsKey);
        return toolchain switch
        {
            "p_r" => await ContainerLocator.Container.Resolve<CcProprietaryCompileStrategy>()
                .PrAsync(project, fpgaModel),
            "nextpnr" => await ContainerLocator.Container.Resolve<CcNextpnrCompileStrategy>()
                .PrAsync(project, fpgaModel),
            _ => throw new Exception($"Unknown toolchain: {toolchain}")
        };
    }
    
    public async Task CreateNetListJsonAsync(IProjectFile verilog)
    {
        await childProcessService.ExecuteShellAsync("yosys", [
                "-p", "hierarchy -auto-top; proc; opt; memory -nomap; wreduce -memx; opt_clean", "-o",
                $"{verilog.Header}.json", verilog.Header
            ],
            Path.GetDirectoryName(verilog.FullPath)!, "Create Netlist...");
    }

    public void SaveConnections(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        var pcfPath = Path.Combine(project.FullPath, CologneChipSettingsHelper.GetConstraintFile(project));
        
        try
        {
            List<string> lines = [];
            List<string> result = [];
            if (File.Exists(pcfPath))
            {
                lines = [..File.ReadAllLines(pcfPath)];
            }
            
            var pinModels = fpga.PinModels.Where(x => x.Value.ConnectedNode is not null).Select(conn => conn.Value).ToList();
            var pinModelsCache = pinModels.ToList();

            foreach (var line in lines)
            {
                if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
                {
                    result.Add(line);
                    continue;
                }

                if (!line.StartsWith("NET")) continue;
                
                var found = false;
                foreach (var pin in pinModels)
                {
                    if (!line.Contains(pin.ConnectedNode!.Node.Name)) continue;
                        
                    var commentIndex = line.IndexOf('#', StringComparison.Ordinal);
                    var comment = commentIndex != -1 ? line[commentIndex..] : string.Empty;
                    
                    var constraintIndex = line.IndexOf('|');
                    var semicolonIndex = line.IndexOf(';');
                    
                    var constraint = constraintIndex != -1 
                                     && constraintIndex < semicolonIndex
                                     && (constraintIndex < commentIndex || commentIndex < 0 )
                        ? line[constraintIndex..semicolonIndex] : string.Empty;
                    
                    var newLine = $"NET \"{pin.ConnectedNode!.Node.Name}\" Loc =  \"{pin.Pin.Name}\"";
                    
                    if (constraint != string.Empty) newLine += $" {constraint}";
                    
                    newLine += ";";
                    
                    if (commentIndex != -1) newLine += $" {comment}";
                    
                    result.Add(newLine.Trim());
                    pinModelsCache.Remove(pin);
                    found = true;
                    break;
                }

                if (!found)
                {
                    result.Add($"# {line}");
                }
            }

            result.AddRange(pinModelsCache.Select(pin => $"NET \"{pin.ConnectedNode!.Node.Name}\" Loc =  \"{pin.Pin.Name}\";"));
            File.WriteAllLines(pcfPath, result);
            CologneChipSettingsHelper.UpdateProjectOverlay(project);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }
    
    
    public async Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        var start = DateTime.Now;
        outputService.WriteLine($"Starting CC Toolchain... ({
            settingsService.GetSettingValue<string>(CologneChipConstantService.ToolChainSettingsKey)})\n===============");
        
        var success = await SynthAsync(project, fpga);
        success &= await PrAysnc(project, fpga);
        
        var endTime = DateTime.Now - start;
        if (success)
            outputService.WriteLine(
                $"==================\n\nCC Toolchain finished after {(int)endTime.TotalMinutes:D2}:{endTime.Seconds:D2}\n");
        else
            outputService.WriteLine(
                $"==================\n\nCC Toolchain failed after {(int)endTime.TotalMinutes:D2}:{endTime.Seconds:D2}\n",
                Brushes.Red);
        
        return success;
    }
}