using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.CologneChip.Services;

public class CcUtilsService(IPackageService packageService, ISettingsService settingsService, IApplicationStateService applicationStateService, ICcCustomLogger logger)
{
    private Task<(bool success, bool needsRestart)> InstallDependenciesAsync()
	{
		ApplicationProcess checkProc = applicationStateService.AddState("Checking dependencies", AppState.Loading);
		
		bool restartRequired = false;
		bool globalSuccess = true;

		(string id, Version minversion)[] dependencyIDs =
		[
			("OneWare.GhdlExtension", new Version(0, 10, 7)),
			("osscadsuite", new Version(2025, 01, 21)),
			("ghdl", new Version(5, 0, 1))
		];

		// Install osscadsuite binary between GHDL plugin and ghdl binary to allow for the addition of the ghdl binary to the store

		foreach ((string dependencyId, Version minVersion) in dependencyIDs)
		{
			IPackageState? dependencyModel = packageService.Packages.GetValueOrDefault(dependencyId);
			Package? dependencyPackage = dependencyModel?.Package;

			if (dependencyPackage == null)
			{
				logger.Error(
					$"Dependency with ID {dependencyId} is not available in the package manager. Please file a bug report if this issue persists");

				globalSuccess = false;
				continue;
			}

			if (packageService.Packages.GetValueOrDefault(dependencyId) is
			    {
				    Status: PackageStatus.Available
			    })
			{
				bool updatePerformed = true;

				if (settingsService.GetSettingValue<bool>(CologneChipConstantService.AutoDownloadBinariesKey))
				{
					logger.Log($"Installing \"{dependencyPackage.Name}\"...", true);

					bool localSuccess = false;

					// Try to install the dependency, starting with the latest version
					// If the version is not compatible or the download fails, try the previous version
					foreach (PackageVersion packageVersion in dependencyPackage.Versions!.Reverse())
					{
						

						PackageVersion? installedVersion = dependencyModel?.InstalledVersion;

						if (installedVersion == packageVersion)
						{
							logger.Log(
								$"Failed to update {dependencyPackage.Name} from version {installedVersion.Version} to version {dependencyPackage.Versions!.Last()}",
								true);

							updatePerformed = false;
							localSuccess = true;
							break;
						}

						// localSuccess = await dependencyModel.DownloadAsync(packageVersion);

						// Stop trying, if install has been successful
						if (localSuccess)
						{
							break;
						}
					}

					globalSuccess = globalSuccess && localSuccess;

					if (updatePerformed)
					{
						if (localSuccess)
						{
							logger.Log($"Successfully installed \"{dependencyPackage.Name}\".", true);
						}
						else
						{
							logger.Error($"Failed to install \"{dependencyPackage.Name}\".");
						}
					}
				}
				else
				{
					// Log an error if the user has not enabled automatic binary downloads

					logger.Error(
						$"Extension \"{dependencyPackage.Name}\" is not installed. Please enable \"Automatically download Binaries\" under the \"Experimental\" settings or download the extension yourself");

					globalSuccess = false;
				}

				if (globalSuccess && updatePerformed)
				{
					restartRequired = true;
				}
			}

			// Check whether now the correct version is installed
			if (dependencyModel!.Status is PackageStatus.Installed or PackageStatus.UpdateAvailable)
			{
				if (minVersion.CompareTo(Version.Parse(dependencyModel.InstalledVersion!.Version!)) <= 0)
				{
					logger.Log(
						$"Dependency {dependencyPackage.Id} installed with version {dependencyModel.InstalledVersion.Version} greater than or equal to expected version {minVersion.ToString()}");
				}
				else
				{
					logger.Error(
						$"Installed version {dependencyModel.InstalledVersion.Version} for {dependencyPackage.Name} is below the minimum version {minVersion.ToString()}. Please update {dependencyPackage.Name}!");

					globalSuccess = false;
				}
			}
			else if (dependencyModel.Status is PackageStatus.NeedRestart)
			{
				restartRequired = true;
			}
		}

		if (globalSuccess && restartRequired)
		{
			logger.Log("Dependencies were successfully installed. Please restart OneWare Studio!", true);
		}

		applicationStateService.RemoveState(checkProc);

		return Task.FromResult((globalSuccess, restartRequired));
	}
}