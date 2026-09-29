namespace Ariadne.Extensions.IO;

public static class DirectoryInfoExtensions
{
    public static void MergeTo(this DirectoryInfo directoryInfo, string destDirName)
    {
        if (!Directory.Exists(destDirName))
        {
            directoryInfo.MoveTo(destDirName);
            return;
        }
        foreach (var file in directoryInfo.GetFiles())
        {
            var destFileName = Path.Join(destDirName, file.Name);
            file.MoveTo(destFileName, true);
        }
        foreach (var subdir in directoryInfo.GetDirectories())
        {
            var destSubDirName = Path.Join(destDirName, subdir.Name);
            subdir.MergeTo(destSubDirName);
        }
    }
}
