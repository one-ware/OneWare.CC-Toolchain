using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.OssCadSuiteIntegration.Helpers;

namespace OneWare.CologneChip.ViewModels;

public class CologneChipSetupViewModel : FlexibleWindowViewModelBase
{
    private readonly IHttpService _httpService;
    private readonly IPackageService _packageService;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private const string GateMateDataUrl = 
        "https://raw.githubusercontent.com/swittlich/OneWare.GateMate/main/oneware-package.json";

    private CancellationTokenSource? _installCts;
    
    public CologneChipSetupViewModel(IHttpService httpService, IPackageService packageService)
    {
        _httpService =  httpService;
        _packageService = packageService;
        CancelCommand = new RelayCommand<object>(Cancel);
        InstallCommand = new AsyncRelayCommand<object>(InstallAsync);
        AttachedToVisualTreeCommand = new AsyncRelayCommand(AttachedToVisualTreeAsync);
    }
    
    public string Header { get; } = "Welcome to Cologne Chip";
    public string Description { get; } = "Get started with One Ware Studio and the Cologne Chip plugin by installing the required packages.";
    public ObservableCollection<IPackageState> RequiredPackages { get; } = [];
    
    public ICommand AttachedToVisualTreeCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand InstallCommand { get; }

    public bool IsLoading
    {
        get;
        set => SetProperty(ref field, value);
    }
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
    public bool CanInstall => !IsLoading && !LoadingFailed;

    private async Task AttachedToVisualTreeAsync()
    {
        IsLoading = true;
        
        try
        {
            var downloadManifest = await _httpService.DownloadTextAsync(GateMateDataUrl);
            var package = JsonSerializer.Deserialize<Package>(downloadManifest!, _serializerOptions);
            if (package == null)
                throw new Exception();
            
            RequiredPackages.Add(_packageService.Packages[OssCadSuiteHelper.OssCadPackage.Id!]);
            RequiredPackages.Add(_packageService.Packages[package.Id!]);
        }
        catch
        {
            LoadingFailed = true;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(CanInstall));
        }
    }

    private void AddPackage(Package package)
    {
        RequiredPackages.Add(_packageService.Packages[package.Id!]);
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
        bool cancellationRequested;
        
        try
        {
            foreach (var package in RequiredPackages)
            {
                _installCts.Token.ThrowIfCancellationRequested();
                var result = await _packageService.InstallAsync(package.Package, null, false, false, _installCts.Token);
                
                if (!success)
                    continue;
                
                success = result.Status is PackageInstallResultReason.AlreadyInstalled or PackageInstallResultReason.Installed;
            }
        }
        catch
        {
            success = false;
        }
        finally
        {
            IsInstalling = false;
            cancellationRequested = _installCts.IsCancellationRequested;
            _installCts?.Dispose();
            _installCts = null;
        }

        if (!success && !cancellationRequested)
        {
            await ContainerLocator.Container.Resolve<IWindowService>().ShowMessageAsync("Installation failed",
                "Please try again later or check for OneWare Studio updates", MessageBoxIcon.Error);
        }
        Cancel(control);
    }
}