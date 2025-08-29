using Avalonia.Media;
using Avalonia.Threading;
using OneWare.CologneChip.Helpers;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.GhdlExtension.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using Prism.Ioc;

namespace OneWare.CologneChip.Services;

public class CcProprietaryCompileStrategy : CcCompileStrategyBase
{
    protected override IEnumerable<string> BuildYosysArgs(string topName, string topLang, string topHeader, string yosysSynthTool)
    {
        switch (topLang)
        {
            case "vhd":
                Out.WriteLine("VHDL Synthesis...\n===============");
                return new[] { "-q", "-l", "./synth.log",
                    "-p", $"ghdl --warn-no-binding -C --ieee=synopsys ./../{topHeader} -e {topName}; " +
                          $"{yosysSynthTool} -nomx8 -top {topName} -vlog {topName}_synth.v" };
            case "v":
                Out.WriteLine("Verilog Synthesis...\n==============");
                return new[] { "-ql", "./synth.log",
                    "-p", $"{yosysSynthTool} -nomx8 -top {topName} -vlog {topName}_synth.v" };
            default:
                throw new NotSupportedException($"Unsupported top language: {topLang}");
        }
    }

    protected override (string exe, List<string> args) BuildPrCommand(string topName, string topLang, string ccfFile)
    {
        return ("p_r", new List<string> { "-i", $"{topName}_synth.v", "-o", topName, $"-ccf ./../{ccfFile} -cCP" });
    }
    
    public override Task<bool> PackAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
        => Task.FromResult(true);
}