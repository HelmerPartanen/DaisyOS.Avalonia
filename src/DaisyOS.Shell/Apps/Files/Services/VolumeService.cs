using System;
using System.Collections.Generic;
using System.IO;

namespace DaisyOS.Shell.Apps.Files.Services;

public record VolumeInfo(string Name, string Path, string Type, bool IsRemovable);

public static class VolumeService
{
    public static List<VolumeInfo> GetVolumes()
    {
        var volumes = new List<VolumeInfo>();
        try
        {
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (drive.IsReady && drive.DriveType != DriveType.Ram)
                {
                    bool isRemovable = drive.DriveType == DriveType.Removable || drive.DriveType == DriveType.CDRom || drive.Name.StartsWith("/run/media") || drive.Name.StartsWith("/media");
                    string name = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? Path.GetFileName(drive.Name.TrimEnd(Path.DirectorySeparatorChar)) : drive.VolumeLabel;
                    if (string.IsNullOrEmpty(name)) name = drive.Name;
                    
                    volumes.Add(new VolumeInfo(name, drive.Name, drive.DriveFormat, isRemovable));
                }
            }
        }
        catch { }

        return volumes;
    }
}
