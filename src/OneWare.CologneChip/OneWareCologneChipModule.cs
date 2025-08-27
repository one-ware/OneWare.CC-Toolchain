using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using OneWare.CologneChip.Helpers;
using OneWare.CologneChip.Services;
using OneWare.CologneChip.ViewModels;
using OneWare.CologneChip.Views;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Models;
using Prism.Ioc;
using Prism.Modularity;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.CologneChip;

public class OneWareCologneChipModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<CologneChipService>();
        containerRegistry.RegisterSingleton<CcProprietaryCompileStrategy>();
    }
    
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var settingsService = containerProvider.Resolve<ISettingsService>();
        var projectExplorerService = containerProvider.Resolve<IProjectExplorerService>();
        var cologneChipService = containerProvider.Resolve<CologneChipService>();
        var fpgaService = containerProvider.Resolve<FpgaService>();
        
        var defaultCologneChipPath = "./";
        
        var resourceInclude = new ResourceInclude(new Uri("avares://OneWare.CologneChip/Styles/Icons.axaml")) 
            {Source = new Uri("avares://OneWare.CologneChip/Styles/Icons.axaml")};
        Application.Current?.Resources.MergedDictionaries.Add(resourceInclude);
        
        containerProvider.Resolve<IFileIconService>().RegisterFileIcon("VsImageLib2019.SettingsFile16X", ".ccf");

        containerProvider.Resolve<IProjectExplorerService>().RegisterConstructContextMenu((x, l) =>
        {
            if (x is [IProjectFile { Extension: ".ccf" } ccf])
            {
                if (ccf.Root is UniversalFpgaProjectRoot universalFpgaProjectRoot)
                {
                    if (CologneChipSettingsHelper.GetConstraintFile(universalFpgaProjectRoot) == ccf.RelativePath) {
                        l.Add(new MenuItemViewModel("ccf")
                        {
                            Header = "Unset as Projects Constraint File",
                            Command = new AsyncRelayCommand(() => CologneChipSettingsHelper.UpdateProjectProperties(ccf)),
                        });
                    }
                    else
                    {
                        l.Add(new MenuItemViewModel("ccf")
                        {
                            Header = "Set as Projects Constraint File",
                            Command = new AsyncRelayCommand(() => CologneChipSettingsHelper.UpdateProjectProperties(ccf)),
                            
                        });
                    }
                }
            }
        });
        
        containerProvider.Resolve<IWindowService>().RegisterUiExtension("UniversalFpgaToolBar_DownloaderConfigurationExtension", new UiExtension(x =>
        {
            if (x is not UniversalFpgaProjectRoot cm) return null;
            return new CologneChipLoaderWindowExtensionView()
            {
                DataContext = containerProvider.Resolve<CologneChipLoaderWindowExtensionViewModel>((typeof(UniversalFpgaProjectRoot), cm))
            };
        }));
        
        
        containerProvider.Resolve<FpgaService>().RegisterToolchain<CologneChipToolchain>();
        containerProvider.Resolve<FpgaService>().RegisterLoader<CologneChipLoader>();
        containerProvider.Resolve<IProjectExplorerService>().Projects.CollectionChanged += CologneChipSettingsHelper.OnCollectionChanged;
        containerProvider.Resolve<IPackageService>().RegisterPackage(CologneChipConstantService.CologneChipPackage);
        
        settingsService.RegisterSetting("Tools", "CologneChip", CologneChipConstantService.CcPathSetting, 
            new FolderPathSetting("CologneChip Toolchain Path", defaultCologneChipPath, null, null, IsCologneChipPathValid));
        
        settingsService.RegisterSetting("Tools", "CologneChip", CologneChipConstantService.ToolChainSettingsKey,
            new ComboBoxSetting("Place & Route", CologneChipConstantService.ToolChainDefault, CologneChipConstantService.Toolchains));
        
        settingsService.GetSettingObservable<string>(CologneChipConstantService.CcPathSetting).Subscribe(x =>
        {
            if (string.IsNullOrEmpty(x)) return;

            if (!IsCologneChipPathValid(x))
            {
                containerProvider.Resolve<ILogger>().Warning("CologneChip Toolchain path invalid", null, false);
                return;
            }
            
            var yosys = Path.Combine(x, "bin/yosys");
            var pr = Path.Combine(x, "bin/p_r");
            var openFpgaLoader = Path.Combine(x, "bin/openFPGALoader");
            
            ContainerLocator.Container.Resolve<IEnvironmentService>().SetPath("CC_yosys", yosys);
            ContainerLocator.Container.Resolve<IEnvironmentService>().SetPath("CC_p_r", pr);
            ContainerLocator.Container.Resolve<IEnvironmentService>().SetPath("CC_openFPGALoader", openFpgaLoader);
        });
        
        containerProvider.Resolve<ISettingsService>().RegisterSetting("Tools", "CologneChip", 
            CologneChipConstantService.CologneChipSettingsIgnoreGuiKey, new CheckBoxSetting("Ignore UI for HardwarePin Mapping", false));
        
        containerProvider.Resolve<ISettingsService>().RegisterSetting("Tools", "CologneChip", 
            CologneChipConstantService.CologneChipSettingsIgnoreSynthExitCode, new CheckBoxSetting("Ignore an exit code not equal to 0 after the synthesis", false));

        
        
        containerProvider.Resolve<IWindowService>().RegisterUiExtension("UniversalFpgaToolBar_CompileMenuExtension",
            new UiExtension(
                x =>
                {
                    if (x is not UniversalFpgaProjectRoot { Toolchain: CologneChipToolchain } root) return null;

                    var name = root.Properties["Fpga"]?.ToString();
                    var fpgaPackage = fpgaService.FpgaPackages.FirstOrDefault(obj => obj.Name == name);
                    var fpga = fpgaPackage?.LoadFpga();
                    
                    return new StackPanel()
                    {
                        Orientation = Orientation.Vertical,
                        Children =
                        {
                            new MenuItem()
                            {
                                Header = "Run Synthesis",
                                Command = new AsyncRelayCommand(async () =>
                                {
                                    // await projectExplorerService.SaveOpenFilesForProjectAsync(root);
                                    await cologneChipService.SynthAsync(root, new FpgaModel(fpga!));
                                }, () => fpga != null)
                            },
                            new MenuItem()
                            {
                                Header = "Run Place and Route",
                                Command = new AsyncRelayCommand(async () =>
                                {
                                    // await projectExplorerService.SaveOpenFilesForProjectAsync(root);
                                    await cologneChipService.PrAysnc(root, new FpgaModel(fpga!)); 
                                }, () => fpga != null)
                            },
                        }
                    };
                }));
        
    }
            
    private static bool IsCologneChipPathValid(string path)
    {
        if (!Directory.Exists(path)) return false;
        
        if (!File.Exists(Path.Combine(path, "bin", "VERSION"))) return false;
        if (!Directory.Exists(Path.Combine(path, "bin", "yosys"))) return false;
        if (!Directory.Exists(Path.Combine(path, "bin", "p_r"))) return false;
        if (!Directory.Exists(Path.Combine(path, "bin", "openFPGALoader"))) return false;
        
        return true;
    }
}