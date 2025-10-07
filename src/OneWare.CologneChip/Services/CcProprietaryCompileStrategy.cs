using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.CologneChip.Services;

public class CcProprietaryCompileStrategy : CcCompileStrategyBase
{
    protected override IEnumerable<string> BuildYosysArgs(
        string topName, string topLang, string topHeader, string yosysSynthTool, string? preSynthVerilog)
    {
        if (topLang == "vhd")
        {
            if (preSynthVerilog is not null)
            {
                Out.WriteLine("VHDL Synthesis (external GHDL → Verilog)...\n===============");
                return new[] {
                    "-ql", "./synth.log",
                    "-p", $"read_verilog {preSynthVerilog}; " +
                          $"{yosysSynthTool} -nomx8 -top {topName} -vlog {topName}_synth.v"
                };
            }
            else
            {
                Out.WriteLine("VHDL Synthesis (embedded GHDL)...\n===============");
                return new[] {
                    "-ql", "./synth.log",
                    "-p", $"ghdl --warn-no-binding -C --ieee=synopsys ./../{topHeader} -e {topName}; " +
                          $"{yosysSynthTool} -nomx8 -top {topName} -vlog {topName}_synth.v"
                };
            }
        }

        Out.WriteLine("Verilog Synthesis...\n==============");
        return new[] {
            "-ql", "./synth.log",
            "-p", $"{yosysSynthTool} -nomx8 -top {topName} -vlog {topName}_synth.v"
        };
    }

    protected override (string exe, List<string> args) BuildPrCommand(string topName, string topLang, string ccfFile)
    {
        return ("p_r", new List<string> { "-i", $"{topName}_synth.v", "-o", topName, $"-ccf ./../{ccfFile} -cCP" });
    }
    
    public override Task<bool> PackAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
        => Task.FromResult(true);
}