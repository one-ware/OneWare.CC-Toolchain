using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.CologneChip.Services;

public interface ICologneChipCompileStrategy
{
    public Task<bool> SynthAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel);

    public Task<bool> PrAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel);
}