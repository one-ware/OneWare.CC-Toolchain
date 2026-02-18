using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneWare.CologneChip.Helpers;
using OneWare.CologneChip.Services;
using OneWare.CologneChip.ViewModels;
using OneWare.CologneChip.Views;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.CologneChip;

// ReSharper disable once UnusedType.Global
public class OneWareCologneChipModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection containerRegistry)
    {
        containerRegistry.AddSingleton<CologneChipService>();
        containerRegistry.AddSingleton<CcProprietaryCompileStrategy>();
        containerRegistry.AddSingleton<CcNextpnrCompileStrategy>();
        containerRegistry.AddSingleton<CcSettingsService>();
        containerRegistry.AddSingleton<CcUtilsService>();
    }
    
    public override void Initialize(IServiceProvider containerProvider)
    {
        var settingsService = containerProvider.Resolve<ISettingsService>();
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
                        l.Add(new MenuItemModel("ccf")
                        {
                            Header = "Unset as Projects Constraint File",
                            Command = new AsyncRelayCommand(() => CologneChipSettingsHelper.UpdateProjectProperties(ccf)),
                        });
                    }
                    else
                    {
                        l.Add(new MenuItemModel("ccf")
                        {
                            Header = "Set as Projects Constraint File",
                            Command = new AsyncRelayCommand(() => CologneChipSettingsHelper.UpdateProjectProperties(ccf)),
                        });
                    }
                }
            }
        });
        
        fpgaService.RegisterProjectEntryModification(x =>
        {
            if (x.Root is not UniversalFpgaProjectRoot universalFpgaProjectRoot) return;

            if (x is IProjectFile file && CologneChipSettingsHelper.GetConstraintFile(universalFpgaProjectRoot) ==
                file.RelativePath)
            {
                x.Icon?.AddOverlay("CologneChip", "ForkAwesome.Check");
            }
            else
            {
                x.Icon?.RemoveOverlay("CologneChip");
            }
        });
        
        containerProvider.Resolve<IWindowService>().RegisterUiExtension("UniversalFpgaToolBar_DownloaderConfigurationExtension", new OneWareUiExtension(x =>
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
            new ComboBoxSetting("Place & Route", CologneChipConstantService.ToolChainDefault, CologneChipConstantService.Toolchains.Cast<object>().ToArray()));
        
        settingsService.RegisterSetting("Tools", "CologneChip", CologneChipConstantService.OpenFpgaLoaderSourceSettingsKey,
            new ComboBoxSetting("openFPGALoader Source", CologneChipConstantService.OpenFpgaLoaderSourceDefault, CologneChipConstantService.BinarySources.Cast<object>().ToArray()));
        
        settingsService.RegisterSetting("Tools", "CologneChip", CologneChipConstantService.YosysSourceSettingsKey,
            new ComboBoxSetting("Yosys Source", CologneChipConstantService.OpenFpgaLoaderSourceDefault, CologneChipConstantService.BinarySources.Cast<object>().ToArray()));
        
        settingsService.GetSettingObservable<string>(CologneChipConstantService.CcPathSetting).Subscribe(x =>
        {
            if (string.IsNullOrEmpty(x)) return;

            if (!IsCologneChipPathValid(x))
            {
                containerProvider.Resolve<ILogger>().Warning("CologneChip Toolchain path invalid", null, false);
                return;
            }
            
            ContainerLocator.Container.Resolve<IEnvironmentService>().SetPath("CC_p_r",  Path.Combine(x, "bin/p_r"));

        });

        var projectSettingsService = containerProvider.Resolve<IProjectSettingsService>();
        projectSettingsService.AddProjectSetting(new ProjectSettingBuilder()
            .WithSetting(new ComboBoxSetting("Place & Route", CologneChipConstantService.ProjectOverrideValue,
                CologneChipConstantService.ToolchainsProject.Cast<object>().ToArray()))
            .WithCategory("CologneChip")
            .WithKey(CologneChipConstantService.ToolChainSettingsKey)
            .Build());
        
        projectSettingsService.AddProjectSetting(new ProjectSettingBuilder()
            .WithSetting(new ComboBoxSetting("openFPGALoader Source",
                CologneChipConstantService.ProjectOverrideValue,
                CologneChipConstantService.BinarySourcesProject.Cast<object>().ToArray()))
            .WithCategory("CologneChip")
            .WithKey(CologneChipConstantService.OpenFpgaLoaderSourceSettingsKey)
            .Build());

        projectSettingsService.AddProjectSetting(new ProjectSettingBuilder()
            .WithSetting(new ComboBoxSetting("Yosys Source",
                CologneChipConstantService.ProjectOverrideValue,
                CologneChipConstantService.BinarySourcesProject.Cast<object>().ToArray()))
            .WithCategory("CologneChip")
            .WithKey(CologneChipConstantService.YosysSourceSettingsKey)
            .Build());
        
        containerProvider.Resolve<ISettingsService>().RegisterSetting("Tools", "CologneChip", 
            CologneChipConstantService.CologneChipSettingsIgnoreGuiKey, new CheckBoxSetting("Ignore UI for HardwarePin Mapping", false));
        
        containerProvider.Resolve<ISettingsService>().RegisterSetting("Tools", "CologneChip", 
            CologneChipConstantService.CologneChipSettingsIgnoreSynthExitCode, new CheckBoxSetting("Ignore an exit code not equal to 0 after the synthesis", false));
        
        containerProvider.Resolve<ISettingsService>().RegisterSetting("Tools", "CologneChip", 
            CologneChipConstantService.AutoDownloadBinariesKey, new CheckBoxSetting("Auto Download Binaries", true));
        
        containerProvider.Resolve<IWindowService>().RegisterUiExtension("UniversalFpgaToolBar_CompileMenuExtension",
            new OneWareUiExtension(
                x =>
                {
                    if (x is not UniversalFpgaProjectRoot { Toolchain: "cologneChip" } root) return null;

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
                                    await cologneChipService.SynthAsync(root, new FpgaModel(fpga!));
                                }, () => fpga != null)
                            },
                            new MenuItem()
                            {
                                Header = "Run Place and Route",
                                Command = new AsyncRelayCommand(async () =>
                                {
                                    await cologneChipService.PrAsync(root, new FpgaModel(fpga!)); 
                                }, () => fpga != null)
                            },
                            new MenuItem()
                            {
                                Header = "Run Packing",
                                Command = new AsyncRelayCommand(async () =>
                                {
                                    await cologneChipService.PackAsync(root, new FpgaModel(fpga!)); 
                                }, () => fpga != null)
                            },
                        }
                    };
                }));
        
    }
            
    private static bool IsCologneChipPathValid(string path)
    {
        if (!Directory.Exists(path)) return false;
        
        if (!File.Exists(Path.Combine(path, "VERSION"))) return false;
        if (!Directory.Exists(Path.Combine(path, "bin", "yosys"))) return false;
        if (!Directory.Exists(Path.Combine(path, "bin", "p_r"))) return false;
        if (!Directory.Exists(Path.Combine(path, "bin", "openFPGALoader"))) return false;
        
        return true;
    }
}