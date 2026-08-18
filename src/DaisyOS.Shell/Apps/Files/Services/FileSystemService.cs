using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DaisyOS.Shell.Apps.Files.Services;

public enum ConflictResolution
{
    Replace,
    Skip,
    Cancel
}

public static class FileSystemService
{
    public static async Task<bool> CreateFolderAsync(string parentPath, string folderName)
    {
        return await Task.Run(() =>
        {
            try
            {
                var newPath = Path.Combine(parentPath, folderName);
                if (!Directory.Exists(newPath))
                {
                    Directory.CreateDirectory(newPath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        });
    }

    public static async Task<bool> RenameAsync(string currentPath, string newName)
    {
        return await Task.Run(() =>
        {
            try
            {
                var parent = Path.GetDirectoryName(currentPath);
                if (parent == null) return false;
                
                var newPath = Path.Combine(parent, newName);
                if (File.Exists(newPath) || Directory.Exists(newPath)) return false;

                if (Directory.Exists(currentPath))
                {
                    Directory.Move(currentPath, newPath);
                }
                else if (File.Exists(currentPath))
                {
                    File.Move(currentPath, newPath);
                }
                else return false;

                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public static async Task<bool> MoveToTrashAsync(string path)
    {
        return await Task.Run(() =>
        {
            try
            {
                var trashDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Trash");
                var filesDir = Path.Combine(trashDir, "files");
                var infoDir = Path.Combine(trashDir, "info");

                Directory.CreateDirectory(filesDir);
                Directory.CreateDirectory(infoDir);

                var fileName = Path.GetFileName(path);
                var destPath = Path.Combine(filesDir, fileName);
                var infoPath = Path.Combine(infoDir, $"{fileName}.trashinfo");

                int i = 1;
                while (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    var newName = $"{nameWithoutExt} {i}{ext}";
                    destPath = Path.Combine(filesDir, newName);
                    infoPath = Path.Combine(infoDir, $"{newName}.trashinfo");
                    i++;
                }

                if (Directory.Exists(path))
                {
                    Directory.Move(path, destPath);
                }
                else if (File.Exists(path))
                {
                    File.Move(path, destPath);
                }
                else return false;

                var trashInfo = $"[Trash Info]\nPath={Uri.EscapeDataString(path)}\nDeletionDate={DateTime.Now:yyyy-MM-ddTHH:mm:ss}";
                File.WriteAllText(infoPath, trashInfo);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public static async Task DeletePermanentlyAsync(string path)
    {
        await Task.Run(() =>
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch { }
        });
    }

    public static async Task<bool> CopyAsync(string sourcePath, string destPath, Func<string, Task<ConflictResolution>> onConflict)
    {
        return await Task.Run(async () =>
        {
            try
            {
                if (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    var resolution = await onConflict(destPath);
                    if (resolution == ConflictResolution.Cancel) return false;
                    if (resolution == ConflictResolution.Skip) return true;
                    if (resolution == ConflictResolution.Replace)
                    {
                        if (File.Exists(destPath)) File.Delete(destPath);
                        else if (Directory.Exists(destPath)) Directory.Delete(destPath, true);
                    }
                }

                if (Directory.Exists(sourcePath))
                {
                    Directory.CreateDirectory(destPath);
                    foreach (var file in Directory.GetFiles(sourcePath))
                    {
                        var destFile = Path.Combine(destPath, Path.GetFileName(file));
                        await CopyAsync(file, destFile, onConflict);
                    }
                    foreach (var dir in Directory.GetDirectories(sourcePath))
                    {
                        var destDir = Path.Combine(destPath, Path.GetFileName(dir));
                        await CopyAsync(dir, destDir, onConflict);
                    }
                }
                else if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, destPath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public static async Task<bool> MoveAsync(string sourcePath, string destPath, Func<string, Task<ConflictResolution>> onConflict)
    {
        return await Task.Run(async () =>
        {
            try
            {
                if (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    var resolution = await onConflict(destPath);
                    if (resolution == ConflictResolution.Cancel) return false;
                    if (resolution == ConflictResolution.Skip) return true;
                    if (resolution == ConflictResolution.Replace)
                    {
                        if (File.Exists(destPath)) File.Delete(destPath);
                        else if (Directory.Exists(destPath)) Directory.Delete(destPath, true);
                    }
                }

                if (Directory.Exists(sourcePath))
                {
                    Directory.Move(sourcePath, destPath);
                }
                else if (File.Exists(sourcePath))
                {
                    File.Move(sourcePath, destPath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        });
    }
}
