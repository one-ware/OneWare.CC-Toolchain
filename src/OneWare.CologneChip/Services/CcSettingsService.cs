using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.CologneChip.Services;

public class CcSettingsService(ISettingsService settingsService)
{
    
    public string GetSetting(string key, UniversalFpgaProjectRoot? root = null)
    {
        if (root != null)
        {
            if (root.Properties.ContainsKey(key))
            {
                var value =  root.Properties[key]!.ToString();
                if (value != CologneChipConstantService.ProjectOverrideValue)
                    return value;
            }
        }

        return settingsService.GetSettingValue<string>(key);
    }
}