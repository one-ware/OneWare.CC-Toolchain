using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.CologneChip.Services;

public class CcNextpnrCompileStrategy : ICologneChipCompileStrategy
{
    public Task<bool> SynthAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        throw new NotImplementedException();
    }

    public Task<bool> PrAsync(UniversalFpgaProjectRoot project, FpgaModel fpgaModel)
    {
        throw new NotImplementedException();
    }
}