using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Xml;
using dvsku.Wpf.Utilities.Commands;
using dvsku.Wpf.Utilities.ViewModels;
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using LARC.GUI.Models;
using Microsoft.Win32;

namespace LARC.GUI.ViewModels {
    internal class MainViewModel : PageViewModel {
        private List<Region> regions_;
        private Region currentRegion_;
        private string status_;
        private string userConfigPath_;
        private XmlDocument userConfig_;

        private enum RETURN_VALUES {
            OK = 0,
            STEAM_NOT_FOUND = 1,
            LIB_FOLDERS_NOT_FOUND = 2,
            LOST_ARK_NOT_FOUND = 3,
            USER_CONFIG_NOT_FOUND = 4,
            REGION_ID_NOT_FOUND = 5,
            REGION_ID_NOT_SUPPORTED = 6
        }

        public List<Region> Regions {
            get => regions_;
            set {
                regions_ = value;
                RaisePropertyChanged(nameof(Regions));
            }
        }

        public Region CurrentRegion {
            get => currentRegion_;
            set {
                currentRegion_ = value;
                RaisePropertyChanged(nameof(CurrentRegion));
            }
        }

        public string Status {
            get => status_;
            set {
                status_ = value;
                RaisePropertyChanged(nameof(Status));
            }
        }

        public ICommand ChangeRegionCommand { get; set; }

        public MainViewModel() : base() {
            LoadRegions();
            LoadLostArkConfig();
        }

        protected override void InitCommands() {
            base.InitCommands();
            ChangeRegionCommand = new BasicCommand(ExecuteChangeRegionCommand);
        }

        private void ExecuteChangeRegionCommand() {
            Status = userConfigPath_;
            if (CurrentRegion is null) {
                Status = @"Please select a region.";
                return;
            }

            XmlNode regionId = userConfig_.GetElementsByTagName("RegionID")[0];
            regionId.InnerText = CurrentRegion.Code;

            try {
                userConfig_.Save(userConfigPath_);
                MessageBox.Show($"Region changed to {CurrentRegion.Name}!", "", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            catch (Exception e) {
                MessageBox.Show($"Error while saving config:\n\n{e.Message}", "", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadRegions() {
            Regions = new List<Region>() {
                new Region("Europe West", "WE"),
                new Region("Europe Central", "CE"),
                new Region("North America West", "WA"),
                new Region("North America East", "EA"),
                new Region("South America", "SA")
            };

            CurrentRegion = Regions[3];
        }

        private void LoadLostArkConfig() {
            switch (GetLostArkUserConfig()) {
                case RETURN_VALUES.OK: Status = userConfigPath_; break;
                case RETURN_VALUES.STEAM_NOT_FOUND: Status = @"Steam not found."; return;
                case RETURN_VALUES.LIB_FOLDERS_NOT_FOUND: Status = @"libraryfolders.vdf not found."; return;
                case RETURN_VALUES.LOST_ARK_NOT_FOUND: Status = @"Lost Ark not found."; return;
                case RETURN_VALUES.USER_CONFIG_NOT_FOUND: Status = @"UserOption.xml not found."; return;
                default: Status = @"Unknown error"; return;
            }

            switch (LoadUserConfigXML()) {
                case RETURN_VALUES.OK: Status = userConfigPath_; break;
                case RETURN_VALUES.REGION_ID_NOT_FOUND: Status = @"RegionId not found."; return;
                case RETURN_VALUES.REGION_ID_NOT_SUPPORTED: Status = @"RegionId not supported."; return;
                default: Status = @"Unknown error"; return;
            }
        }

        private RETURN_VALUES GetLostArkUserConfig() {
            string installPath;

            // CHECK 32 BIT FIRST
            installPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", "") as string;

            // CHECK 64 BIT
            if (installPath is null)
                installPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", "") as string;

            if (installPath is null) return RETURN_VALUES.STEAM_NOT_FOUND;

            string library = installPath + @"\steamapps\libraryfolders.vdf";

            if (!File.Exists(library)) return RETURN_VALUES.LIB_FOLDERS_NOT_FOUND;

            // PARSE libraryfolders.vdf
            VToken vdf = VdfConvert.Deserialize(File.ReadAllText(library)).Value;

            string path = string.Empty;
            bool found = false;

            // CHECK WHERE LOST ARK IS INSTALLED
            foreach (VProperty folder in vdf.Children().Skip(1)) {
                foreach (VProperty folderData in folder.Value.Children()) {
                    if (folderData.Key.Equals("path"))
                        path = ((VValue)folderData.Value).Value.ToString();

                    if (folderData.Key.Equals("apps")) {
                        foreach (VProperty apps in folderData.Value.Children()) {
                            if (apps.Key.Equals("1599340")) {
                                found = true;
                                break;
                            }
                        }
                    }

                    if (found) break;
                }

                if (found) break;
            }

            if (!found) return RETURN_VALUES.LOST_ARK_NOT_FOUND;

            string userConfigPath = path + @"\steamapps\common\Lost Ark\EFGame\Config\UserOption.xml";

            if (!File.Exists(userConfigPath)) return RETURN_VALUES.USER_CONFIG_NOT_FOUND;

            userConfigPath_ = userConfigPath;
            return RETURN_VALUES.OK;
        }

        private RETURN_VALUES LoadUserConfigXML() {
            XmlDocument userConfig = new XmlDocument();
            userConfig.Load(userConfigPath_);

            XmlNodeList regionId = userConfig.GetElementsByTagName("RegionID");

            if (regionId.Count != 1) return RETURN_VALUES.REGION_ID_NOT_FOUND;

            Region region = Regions.FirstOrDefault(x => x.Code.Equals(regionId[0].InnerText));

            if (region is null) return RETURN_VALUES.REGION_ID_NOT_SUPPORTED;

            CurrentRegion = region;
            userConfig_ = userConfig;

            return RETURN_VALUES.OK;
        }
    }
}
