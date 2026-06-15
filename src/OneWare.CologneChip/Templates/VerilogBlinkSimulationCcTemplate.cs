using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Helpers;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.CologneChip.Templates;

public class VerilogBlinkSimulationCcTemplate(ILogger logger, IMainDockService mainDockService) : IFpgaProjectTemplate
{
    public const string TemplateName = "Verilog Blink with Simulation (Cologne Chip)";

    public void FillTemplate(UniversalFpgaProjectRoot root)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Templates", "BlinkSimulationVerilog");

        try
        {
            var name = root.Header.Replace(" ", "");
            TemplateHelper.CopyDirectoryAndReplaceString(path, root.FullPath, ("%PROJECTNAME%", name));
            var file = root.AddFile(name + ".v");
            
            root.TopEntity = name;
            
            var file2 = root.AddFile(name + "_tb.v");

            root.AddTestBench(file2.RelativePath);

            root.Board = "GateMate FPGA Evaluation Board";
            
            _ = mainDockService.OpenFileAsync(file.FullPath);
            _ = mainDockService.OpenFileAsync(file2.FullPath);
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
        }
    }

    public string Name => TemplateName;
}