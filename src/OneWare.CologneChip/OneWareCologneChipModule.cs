using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneWare.CologneChip.Templates;
using OneWare.CologneChip.ViewModels;
using OneWare.CologneChip.Views;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Helpers;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.CologneChip;

public class OneWareCologneChipModule : OneWareModuleBase
{
    private const string FirstInstallSettingKey = "CologneChip_FirstInstall";
    
    public override void RegisterServices(IServiceCollection containerRegistry)
    {
        
    }
    
    public override void Initialize(IServiceProvider containerProvider)
    {
        var fpgaService = containerProvider.Resolve<FpgaService>();
        fpgaService.RegisterTemplate<VerilogBlinkSimulationCcTemplate>();
        
        var settingsService = containerProvider.Resolve<ISettingsService>();
        settingsService.Register(FirstInstallSettingKey, true);
        
        if (settingsService.GetSettingValue<bool>(FirstInstallSettingKey))
        {
            _ = Dispatcher.UIThread.InvokeAsync(async() =>
            {
                var windowService = containerProvider.Resolve<IWindowService>();
                var packageService = containerProvider.Resolve<IPackageService>();
                var httpService = containerProvider.Resolve<IHttpService>();
                var dataContext = new CologneChipSetupViewModel(httpService, packageService, windowService);

                try
                {
                    await windowService.ShowDialogAsync(new CologneChipSetupView()
                    {
                        DataContext = dataContext,
                    });

                    //set the variable to not show it again in the future
                    settingsService.SetSettingValue(FirstInstallSettingKey, false);
                }
                finally
                {
                    dataContext.Dispose();
                }
            });
        }
    }
}