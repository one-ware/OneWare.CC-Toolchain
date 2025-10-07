using System.Runtime.InteropServices;
using OneWare.CologneChip.Services;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Models;
using OneWare.Essentials.ViewModels;
using OneWare.Settings.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;

namespace OneWare.CologneChip.ViewModels;

public class CologneChipLoaderSettingsViewModel  : FlexibleWindowViewModelBase
{
    private readonly UniversalFpgaProjectRoot _projectRoot;
    private readonly IFpga _fpga;
    
    private readonly ComboBoxSetting _shortTermModeSetting;
    private readonly ComboBoxSetting _longTermModeSetting;
    private readonly ComboBoxSetting _typeSetting;
    private readonly TitledSetting _useWsl;
    
    private readonly Dictionary<string, string> _settings;
    public SettingsCollectionViewModel SettingsCollection { get; } = new("CologneChip Loader Settings")
    {
        ShowTitle = false
    };

    public CologneChipLoaderSettingsViewModel(UniversalFpgaProjectRoot projectRoot, IFpga fpga)
    {
        _projectRoot = projectRoot;
        _fpga = fpga;
        
        Title = "CologneChip Loader Settings";
        Id = "CologneChip Loader Settings";
        
        var defaultProperties = fpga.Properties;
        _settings = FpgaSettingsParser.LoadSettings(projectRoot, fpga.Name);
        
        _typeSetting = new ComboBoxSetting("Type",
            defaultProperties.GetValueOrDefault(CologneChipConstantService.CologneChipTypeKey) ?? "", 
            ["EVB", "Programmer", "OlimexEVB"])
        {
            MarkdownDocumentation = "Board Type?"
        };

        _shortTermModeSetting = new ComboBoxSetting("Short Term Mode",
            defaultProperties.GetValueOrDefault(CologneChipConstantService.CologneChipShortTermModeKey) ?? "",
            ["JTAG", "SPI"])
        {
            MarkdownDocumentation = "Mode to use for Short Term Programming"
        };
        
        _longTermModeSetting = new ComboBoxSetting("Long Term Mode",
            defaultProperties.GetValueOrDefault(CologneChipConstantService.CologneChipLongTermModeKey) ?? "", ["JTAG", "SPI"])
        {
            MarkdownDocumentation = "Mode to use for Long Term Programming",
        };

        bool.TryParse(defaultProperties.GetValueOrDefault(CologneChipConstantService.CologneChipSettingsUseWsl), out var defaultUseWsl);
        
        _useWsl = new CheckBoxSetting("Use WSL (BETA Feature - Windows only)", defaultUseWsl);
        
        if (_settings.TryGetValue(CologneChipConstantService.CologneChipShortTermModeKey, out var qPstMode))
            _typeSetting.Value = qPstMode;
        
        if (_settings.TryGetValue(CologneChipConstantService.CologneChipLongTermModeKey, out var qPstOperation))
            _longTermModeSetting.Value = qPstOperation;
        
        if (_settings.TryGetValue(CologneChipConstantService.CologneChipTypeKey, out var qPstType))
            _typeSetting.Value = qPstType;
        
        if (_settings.TryGetValue(CologneChipConstantService.CologneChipSettingsUseWsl, out var useWsl))
            _useWsl.Value = useWsl;
        
        SettingsCollection.SettingModels.Add(_typeSetting);
        SettingsCollection.SettingModels.Add(_shortTermModeSetting);
        SettingsCollection.SettingModels.Add(_longTermModeSetting);
        
        //Add WSL Setting only on Windows
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            SettingsCollection.SettingModels.Add(_useWsl);
    }
    
    public void Save(FlexibleWindow flexibleWindow)
    {
        _settings[CologneChipConstantService.CologneChipShortTermModeKey] = _shortTermModeSetting.Value.ToString()!;
        _settings[CologneChipConstantService.CologneChipLongTermModeKey] = _longTermModeSetting.Value.ToString()!;
        _settings[CologneChipConstantService.CologneChipTypeKey] = _typeSetting.Value.ToString()!;
        _settings[CologneChipConstantService.CologneChipSettingsUseWsl] = _useWsl.Value.ToString()!;
        
        FpgaSettingsParser.SaveSettings(_projectRoot, _fpga.Name, _settings);
        
        Close(flexibleWindow);
    }
    
    public void Reset()
    {
        foreach (var setting in SettingsCollection.SettingModels)
        {
            setting.Value = setting.DefaultValue;
        }
    }
}