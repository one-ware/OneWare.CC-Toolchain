using Avalonia.Media;
using Avalonia.Threading;
using OneWare.CologneChip.Helpers;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using Prism.Ioc;

namespace OneWare.CologneChip.Services;

public class CcNextpnrCompileStrategy : CcCompileStrategyBase
{
    protected override IEnumerable<string> BuildYosysArgs(string topName, string topLang, string topHeader, string yosysSynthTool)
    {
        switch (topLang)
        {
            case "vhd":
                Out.WriteLine("VHDL Synthesis...\n===============");
                return new[] { "-q", "-l", "./synth.log",
                    "-p", $"ghdl --warn-no-binding -C --ieee=synopsys ./../{topHeader} -e {topName}; " +
                          $"{yosysSynthTool} -nomx8 -top {topName} -luttree -nomult; write_json {topName}.json;" };
            case "v":
                Out.WriteLine("Verilog Synthesis...\n==============");
                return new[] { "-p", $"synth_gatemate -nomx8 -top {topName} -luttree -nomult; write_json {topName}.json;" };
            default:
                throw new NotSupportedException($"Unsupported top language: {topLang}");
        }
    }

    protected override (string exe, List<string> args) BuildPrCommand(string topName, string topLang, string ccfFile)
    {
        return ("nextpnr-himbaechel",
            new List<string> {
                "--device=CCGM1A1",
                "--json", $"{topName}.json",
                "-o", $"ccf=./../{ccfFile}",
                "-o", $"out={topName}_impl.txt",
                "--router=router2"
            });
    }

    public override async Task<bool> PackAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        var start = DateTime.Now;
        var (topName, _) = SplitTop(project.TopEntity?.Header ?? throw new Exception("TopEntity not set!"));

        var (success, _) = await ExecWithOutput(
            "gmpack",
            new List<string> { $"{topName}_impl.txt", $"{topName}.bit" },
            $"{project.FullPath}/build",
            "Running Pack...");

        WritePhaseResult("Pack", start, success);
        return success;
    }
}