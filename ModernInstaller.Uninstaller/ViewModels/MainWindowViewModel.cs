﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace ModernInstaller.ViewModels;

public partial class MainWindowViewModel : ObservableValidator
{
    [ObservableProperty] private bool nowBeforeInstall = true;
    [ObservableProperty] private bool nowInstall = false;
    [ObservableProperty] private int nowProgress =100;
    [ObservableProperty] private bool nowAfterInstall = false;
    
    [ObservableProperty] private string appName="Modern Installer";
    
    private string MainFileFullPath =string.Empty;
    private string Path =string.Empty;
    public MainWindowViewModel()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        using ( var manifestResourceStream = assembly.GetManifestResourceStream("ModernInstaller.Uninstaller.Assets.ApplicationUUID"))
        {
            var bytes = new byte[manifestResourceStream.Length];
            manifestResourceStream.ReadExactly(bytes, 0, bytes.Length);
            var s = Encoding.UTF8.GetString(bytes);
            Console.WriteLine(s);
            using (var openSubKey = Registry.LocalMachine.OpenSubKey(
                       "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\",
                       RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (openSubKey is null)
                {
                    Console.WriteLine("openSubKey is null");
                }
                using (var registryKey = openSubKey.OpenSubKey($$"""{{{s}}}_ModernInstaller"""))
                {
                    if (registryKey is null)
                    {
                        Console.WriteLine("registryKey is null");
                    }
                    AppName= openSubKey.GetValue("DisplayName").ToString();
                    MainFileFullPath=openSubKey.GetValue("Path").ToString()+"\\"+openSubKey.GetValue("MainFile").ToString();
                    Path=openSubKey.GetValue("Path").ToString();
                }
            }
            
            // ZipFile.ExtractToDirectory(manifestResourceStream,InstallPath,true);
        }
       
    }
    [RelayCommand]
    private async Task Install()
    {
        NowBeforeInstall = false;
        NowInstall = true;
        var minProgress = 100;
        await Task.Delay(500);
        var timer = new Timer(10);
        timer.AutoReset = true;
        timer.Elapsed += (sender, args) =>
        {
            
            if (NowProgress>=minProgress)
            {
                NowProgress--;
            }
        };
        timer.Start();
        Task.Run((() =>
        {
            minProgress = 70;
            //检查进程是否退出
            foreach (var e in Process.GetProcesses())
            {
                try
                {
                    if (e.MainModule != null && e.MainModule.FileName == MainFileFullPath)
                    {
                        e.Kill();
                        break;
                    }
                }
                catch (Exception exception)
                {

                }

            }

            minProgress = 50;
            try
            {
                Directory.Delete(Path, true);
            }
            catch (Exception exception)
            {
                return;
            }
            
           
            minProgress = 0;
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (var manifestResourceStream =
                   assembly.GetManifestResourceStream("ModernInstaller.Uninstaller.Assets.ApplicationUUID"))
            {
                var bytes = new byte[manifestResourceStream.Length];
                manifestResourceStream.ReadExactly(bytes, 0, bytes.Length);
                var s = Encoding.UTF8.GetString(bytes);
                using (var openSubKey = Registry.LocalMachine.OpenSubKey(
                           "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\",
                           RegistryKeyPermissionCheck.ReadWriteSubTree))
                {

                    openSubKey.DeleteSubKey($$"""{{{s}}}_ModernInstaller""");
                }
                // ZipFile.ExtractToDirectory(manifestResourceStream,InstallPath,true);
            }
            File.Delete($"{Environment.GetFolderPath(Environment.SpecialFolder.Programs)}\\{AppName}.lnk");
            File.Delete($"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\{AppName}.lnk");
            NowInstall = false;
            NowAfterInstall = true;
        }));

    }

    [RelayCommand]
    private void Exit()
    {
       Environment.Exit(0);
    }
}