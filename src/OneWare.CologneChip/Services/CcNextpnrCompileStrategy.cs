using Avalonia.Media;
using Avalonia.Threading;
using OneWare.CologneChip.Helpers;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;

namespace OneWare.CologneChip.Services;

public class CcNextpnrCompileStrategy : CcCompileStrategyBase
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
                          $"synth_gatemate -nomx8 -top {topName} -luttree -nomult; write_json {topName}.json;"
                };
            }
            else
            {
                Out.WriteLine("VHDL Synthesis (embedded GHDL)...\n===============");
                return new[] {
                    "-ql", "./synth.log",
                    "-p", $"ghdl --warn-no-binding -C --ieee=synopsys ./../{topHeader} -e {topName}; " +
                          $"{yosysSynthTool} -nomx8 -top {topName} -luttree -nomult; write_json {topName}.json;"
                };
            }
        }

        Out.WriteLine("Verilog Synthesis...\n==============");
        return new[] {
            "-p", $"synth_gatemate -nomx8 -top {topName} -luttree -nomult; write_json {topName}.json;"
        };
    }

    protected override (string exe, List<string> args) BuildPrCommand(string topName, string topLang, string ccfFile, string device)
    {
        return ("nextpnr-himbaechel",
            new List<string> {
                $"--device={device}",
                "--json", $"{topName}.json",
                "-o", $"ccf=./../{ccfFile}",
                "-o", $"out={topName}_impl.txt",
                "--router=router2"
            });
    }

    public override async Task<bool> PackAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        var start = DateTime.Now;
        var (topName, _) = SplitTop(project.TopEntity ?? throw new Exception("TopEntity not set!"));

        var (success, _) = await ExecWithOutput(
            "gmpack",
            new List<string> { $"{topName}_impl.txt", $"{topName}.bit" },
            $"{project.FullPath}/build",
            "Running Pack...");

        WritePhaseResult("Pack", start, success);
        return success;
    }
}