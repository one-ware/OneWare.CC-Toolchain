using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using OneWare.CologneChip.ViewModels;
using OneWare.CologneChip.Views;
using OneWare.Essentials.Services;

namespace OneWare.CologneChip;

public class OneWareCologneChipModule : OneWareModuleBase
{
    private const string FirstInstallSettingKey = "CologneChip_FirstInstall";
    
    public override void RegisterServices(IServiceCollection containerRegistry)
    {
        
    }
    
    public override void Initialize(IServiceProvider containerProvider)
    {
        var settingsService = containerProvider.Resolve<ISettingsService>();
        settingsService.Register(FirstInstallSettingKey, true);
        
        if (settingsService.GetSettingValue<bool>(FirstInstallSettingKey))
        {
            _ = Dispatcher.UIThread.InvokeAsync(async() =>
            {
                var packageService = containerProvider.Resolve<IPackageService>();
                var httpService = containerProvider.Resolve<IHttpService>();
                
                await containerProvider.Resolve<IWindowService>().ShowDialogAsync(new CologneChipSetupView()
                {
                    DataContext = new CologneChipSetupViewModel(httpService, packageService),
                });
                
                //set the variable to not show it again in the future
                settingsService.SetSettingValue(FirstInstallSettingKey, false);
            });
        }
    }
}