using Avalonia.Controls.ApplicationLifetimes;
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
    public override void RegisterServices(IServiceCollection containerRegistry)
    {
        
    }

    public override void Initialize(IServiceProvider containerProvider)
    {
        var fpgaService = containerProvider.Resolve<FpgaService>();
        fpgaService.RegisterTemplate<VerilogBlinkSimulationCcTemplate>();
        
        var applicationStateService = containerProvider.Resolve<IApplicationStateService>();

        applicationStateService.RegisterUrlLaunchAction("colognechip", x =>
        {
            if (x is "/setup")
            {
                _ = Dispatcher.UIThread.InvokeAsync(async () =>
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
                    }
                    finally
                    {
                        dataContext.Dispose();
                    }
                });
            }
        });
    }
}