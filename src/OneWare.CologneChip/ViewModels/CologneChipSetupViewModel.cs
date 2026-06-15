using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using OneWare.CologneChip.Templates;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.OssCadSuiteIntegration.Helpers;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.UniversalFpgaProjectSystem.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Views;

namespace OneWare.CologneChip.ViewModels;

public sealed class CologneChipSetupViewModel : FlexibleWindowViewModelBase, IDisposable
{
    private readonly IHttpService _httpService;
    private readonly IPackageService _packageService;
    private readonly IWindowService _windowService;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private CancellationTokenSource? _installCts;
    
    public CologneChipSetupViewModel(IHttpService httpService, IPackageService packageService, IWindowService windowService)
    {
        _httpService =  httpService;
        _packageService = packageService;
        _windowService = windowService;
        CancelCommand = new RelayCommand<object>(Cancel);
        InstallCommand = new AsyncRelayCommand<object>(InstallAsync);
        AttachedToVisualTreeCommand = new AsyncRelayCommand(AttachedToVisualTreeAsync);
        TryAgainCommand = new AsyncRelayCommand(TryAgainAsync);
        _ = InitializeAsync();
    }
    
    public string Header { get; } = "Welcome to Cologne Chip";
    public string Description { get; } = "Get started with One Ware Studio and the Cologne Chip plugin by installing the required packages.";
    public ObservableCollection<PackageViewModel> RequiredPackages { get; } = [];
    
    public ICommand AttachedToVisualTreeCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand TryAgainCommand { get; }

    public bool IsLoading
    {
        get;
        set => SetProperty(ref field, value);
    } = true;
    public bool LoadingFailed
    {
        get;
        set => SetProperty(ref field, value);
    }
    public bool IsInstalling
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool PackagesAlreadyInstalled
    {
        get;
        set => SetProperty(ref field, value);
    }
    public bool CanInstall => !IsLoading && !LoadingFailed;

    public override bool OnWindowClosing(FlexibleWindow window)
    {
        _installCts?.Cancel();
        return base.OnWindowClosing(window);
    }
    
    public void Dispose()
    {
        _installCts?.Dispose();
    }
    
    private async Task InitializeAsync()
    {
        IsLoading = true;

        if(!_packageService.IsLoaded)
            await _packageService.RefreshAsync();
        
        try
        {
            _packageService.Packages.TryGetValue("OneWare.GateMate", out var gateMateState);
            
            var gateMatePackage = gateMateState?.Package;
            if (gateMatePackage == null)
                throw new Exception();

            List<Package> requiredPackages = [gateMatePackage, OssCadSuiteHelper.OssCadPackage];
            foreach (var pk in requiredPackages)
            {
                PackageViewModel vm = new(pk, _packageService.Packages[pk.Id!]);
                await vm.InitializeAsync(_packageService);
                RequiredPackages.Add(vm);
            }
            PackagesAlreadyInstalled = RequiredPackages.All(x => x.State.Status is PackageStatus.Installed);
        }
        catch (Exception ex)
        {
            LoadingFailed = true;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(CanInstall));
        }
    }

    private async Task AttachedToVisualTreeAsync()
    {
        // timeout, wait 10s to display an error
        for (int i = 0; i < 10; i++)
        {
            if (!IsLoading || LoadingFailed)
                return;

            await Task.Delay(1000);
        }
        IsLoading = false;
        LoadingFailed = true;
    }

    private async Task TryAgainAsync()
    {
        LoadingFailed = false;
        RequiredPackages.Clear();
        await InitializeAsync();
    }

    private void Cancel(object? parameter)
    {
        if (parameter is not Control control)
            return;

        Window? wd = TopLevel.GetTopLevel(control) as Window;
        wd?.Close();
    }
    private async Task InstallAsync(object? parameter)
    {
        if (parameter is not Control control)
            return;
        
        if (IsInstalling) 
            return;

        _installCts = new CancellationTokenSource();
        IsInstalling = true;

        bool success = true;
        bool cancellationRequested = false;

        try
        {
            foreach (var package in RequiredPackages)
            {
                _installCts.Token.ThrowIfCancellationRequested();
                var result = await package.InstallAsync(_packageService, _installCts.Token);
                
                success = result.Status is PackageInstallResultReason.AlreadyInstalled
                    or PackageInstallResultReason.Installed;
                
                if (!success)
                {
                    cancellationRequested = _installCts.Token.IsCancellationRequested;
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            cancellationRequested = true;
            success = false;
        }
        catch
        {
            success = false;
        }
        finally
        {
            IsInstalling = false;
            _installCts?.Dispose();
            _installCts = null;
        }

        if (!success && !cancellationRequested)
        {
            await ContainerLocator.Container.Resolve<IWindowService>().ShowMessageAsync("Installation failed",
                "Please try again later or check for OneWare Studio updates", MessageBoxIcon.Error);
        }
        
        Window? wd = TopLevel.GetTopLevel(control) as Window;
        wd?.Close();

        // open the fpga template dialog with the cologne chip template as default
        var dataContext = ContainerLocator.Container.Resolve<UniversalFpgaProjectCreatorViewModel>();
        dataContext.SettingsCollection.SettingModels
            .OfType<TitledSetting>()
            .First(x => string.Equals(x.Title, "template", StringComparison.OrdinalIgnoreCase)).Value 
                = VerilogBlinkSimulationCcTemplate.TemplateName;
        await _windowService.ShowDialogAsync(new UniversalFpgaProjectCreatorView
        {
            DataContext = dataContext
        });
    }
}

public sealed class PackageViewModel : ObservableObject
{
    public PackageViewModel(Package package, IPackageState packageState)
    {
        Package = package;
        State = packageState;
    }
    public Package Package { get; }
    public IPackageState State { get; }
    
    public IImage? Image
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public int Progress
    {
        get;
        set => SetProperty(ref field, value);
    }

    public async Task InitializeAsync(IPackageService packageService)
    {
        Image = await packageService.DownloadPackageIconAsync(Package);
    }

    public async Task<PackageInstallResult> InstallAsync(IPackageService packageService, CancellationToken cancellationToken)
    {
        IDisposable? subscription = null;
            
        try
        {
            subscription = State
                .WhenValueChanged(x => x.Progress)
                .Subscribe(x => Progress = (int)(x * 100));

            return await packageService.InstallAsync(Package, null, false, false, cancellationToken);
        }
        finally
        {
            subscription?.Dispose();
        }
    }
}