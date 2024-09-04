using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ModernInstaller.Models;
using Vanara.Windows.Shell;

namespace ModernInstaller.ViewModels;

public partial class MainWindowViewModel : ObservableValidator
{
    [ObservableProperty] private bool nowUnstall;
    [ObservableProperty] private bool nowBeforeInstall = true;
    [ObservableProperty] private bool nowInstall = false;
    [ObservableProperty] private int nowProgress = 0;
    [ObservableProperty] private bool nowAfterInstall = false;
    
    [ObservableProperty] private bool showDetail;
    [ObservableProperty] private string appName="Modern Installer";
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [ObservableProperty] private bool agreed = false;
    
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [ObservableProperty] private string installPath=$"C:\\Program Files\\";
   
    public bool CanInstall
    {
        get
        {
            if (string.IsNullOrWhiteSpace(InstallPath))
            {
                CantInstallReason ="安装路径为空，请选择安装目录" ;
                OnPropertyChanged(nameof(CantInstallReason));
                return false;
            }
            if (Directory.Exists(InstallPath)&& (Directory.EnumerateDirectories(InstallPath).Any()||  Directory.EnumerateFiles(InstallPath).Any()))
            {
                CantInstallReason ="安装路径不为空，请重新选择" ;
                OnPropertyChanged(nameof(CantInstallReason));
                return false;
            }

            var pathRoot = Path.GetPathRoot(InstallPath);
            if (string.IsNullOrWhiteSpace(pathRoot))
            {
                CantInstallReason ="安装路径错误" ;
                OnPropertyChanged(nameof(CantInstallReason));
                return false;
            }

            if (!Agreed)
            {
                CantInstallReason ="请同意用户协议" ;
                OnPropertyChanged(nameof(CantInstallReason));
                return false;
            }

            return true;
        }
    }
    public string CantInstallReason { get; set; }

    public MainWindowViewModel()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        using (var infoJsonS =
               assembly.GetManifestResourceStream("ModernInstaller.Assets.Installer.info.json"))
        {
            var bytes2 = new byte[infoJsonS.Length];
            infoJsonS.Read(bytes2, 0, bytes2.Length);
            var s2 = Encoding.UTF8.GetString(bytes2);
            var deserialize = JsonSerializer.Deserialize<Info>(s2,SourceGenerationContext.Default.Info);
            var appName = deserialize.DisplayName;
            AppName = appName;
            InstallPath= $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\\{appName}";
        }
       
    }
    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task Install()
    {
        NowBeforeInstall = false;
        NowInstall = true;
        var maxProgress = 0;
        await Task.Delay(500);
        var timer = new Timer(10);
        timer.AutoReset = true;
        timer.Elapsed += (sender, args) =>
        {
            
            if (NowProgress<=maxProgress)
            {
                NowProgress++;
            }
        };
        timer.Start();
        Task.Run(() =>
        {
            maxProgress = 50;
            Assembly assembly = Assembly.GetExecutingAssembly();
            Directory.CreateDirectory(InstallPath);
            using (var manifestResourceStream = assembly.GetManifestResourceStream("ModernInstaller.Assets.App.zip"))
            {
                ZipFile.ExtractToDirectory(manifestResourceStream, InstallPath, true);
            }

            maxProgress = 70;
            using (var manifestResourceStream =
                   assembly.GetManifestResourceStream(
                       "ModernInstaller.Assets.Installer.ModernInstaller.Uninstaller.exe"))
            {
                using (var fileStream = new FileStream(Path.Combine(InstallPath, "ModerInstaller.Uninstaller.exe"),
                           FileMode.Create))
                {
                    manifestResourceStream.CopyTo(fileStream);
                }

            }

            maxProgress = 100;
            using (var manifestResourceStream =
                   assembly.GetManifestResourceStream("ModernInstaller.Assets.ApplicationUUID"))
            {
                var bytes = new byte[manifestResourceStream.Length];
                manifestResourceStream.Read(bytes, 0, bytes.Length);
                var s = Encoding.UTF8.GetString(bytes);
                using (var openSubKey = Registry.LocalMachine.OpenSubKey(
                           "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\",
                           RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    using (var registryKey = openSubKey.CreateSubKey($$"""{{{s}}}_ModernInstaller"""))
                    {
                        using (var infoJsonS =
                               assembly.GetManifestResourceStream("ModernInstaller.Assets.Installer.info.json"))
                        {
                            var bytes2 = new byte[infoJsonS.Length];
                            infoJsonS.Read(bytes2, 0, bytes2.Length);
                            var s2 = Encoding.UTF8.GetString(bytes2);
                            var deserialize =
                                JsonSerializer.Deserialize<Info>(s2, SourceGenerationContext.Default.Info);
                            registryKey.SetValue("DisplayName", deserialize.DisplayName);
                            registryKey.SetValue("DisplayVersion", deserialize.DisplayVersion);
                            registryKey.SetValue("Publisher", deserialize.Publisher);
                            registryKey.SetValue("Path", InstallPath);
                            registryKey.SetValue("UninstallString", InstallPath + "\\ModerInstaller.Uninstaller.exe");
                            CanExecutePath = deserialize.CanExecutePath;
                            registryKey.SetValue("MainFile", CanExecutePath);
                            if (string.IsNullOrWhiteSpace(deserialize.DisplayIcon))
                            {
                                registryKey.SetValue("DisplayIcon", InstallPath + "\\" + CanExecutePath + ",0");
                            }
                            else
                            {
                                registryKey.SetValue("DisplayIcon", deserialize.DisplayIcon);
                            }

                            registryKey.SetValue("InstallDate", DateTime.Now.ToString("yyyy-MM-dd"));
                            var shellLink = ShellLink.Create(
                                $"{Environment.GetFolderPath(Environment.SpecialFolder.Programs)}\\{AppName}.lnk",
                                InstallPath + "\\" + CanExecutePath, null, InstallPath,null);
                            shellLink.IconLocation = new IconLocation(InstallPath + "\\" + CanExecutePath, 0);
                            
                            //获取桌面路径
                            var shellLin2k = ShellLink.Create(
                                $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\{AppName}.lnk",
                                InstallPath + "\\" + CanExecutePath, null, InstallPath,null);
                            shellLin2k.IconLocation = new IconLocation(InstallPath + "\\" + CanExecutePath, 0);
                        }
                    }
                }
            }

            NowAfterInstall = true;
            NowInstall = false;
        });


    }

    private string CanExecutePath = "";
    [RelayCommand]
    private void ShowDetailC()
    {
        ShowDetail = !ShowDetail;
    }

    [RelayCommand]
    private async Task PickFolder()
    {
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime classicDesktopStyleApplicationLifetime)
        {
            var tryGetFolderFromPathAsync = await classicDesktopStyleApplicationLifetime.MainWindow.StorageProvider.TryGetFolderFromPathAsync(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            var folders= await classicDesktopStyleApplicationLifetime.MainWindow.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions()
                {
                    SuggestedStartLocation = tryGetFolderFromPathAsync
                });
            if (!folders.Any())
            {
                return; 
            }
            var path = "";
            try
            {
                path=  folders.First().Path.LocalPath;
            }
            catch (Exception e)
            {
                path=  folders.First().Path.OriginalString;
            }
            if (Directory.Exists(path)&& (Directory.EnumerateDirectories(path).Any()||  Directory.EnumerateFiles(path).Any()))
            {
                InstallPath = path+AppName;
            }
            else
            {
                InstallPath = path.TrimEnd('\\');
            }
            
        }
    }

    [RelayCommand]
    private void JustClose()
    {
        Environment.Exit(0);
    }
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr ShellExecute(
        [Optional] IntPtr hwnd,
        [Optional] string? lpOperation,
        string lpFile,
        [Optional] string? lpParameters,
        [Optional] string? lpDirectory,
        int nShowCmd);
    [RelayCommand]
    private void CloseAndLaunch()
    {
        ShellExecute(IntPtr.Zero, "open", CanExecutePath, "", InstallPath,
            1);
        Environment.Exit(0);
    }
}