using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Daedalus.ModManager.GUI.ViewModels;
using FluentIcons.Common;

namespace Daedalus.ModManager.GUI.Converters;

public sealed class FileIconConverter : IValueConverter
{
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) =>
        value switch
        {
            FileLeafNodeViewModel file => ForExtension(Path.GetExtension(file.DisplayName)),
            DeployedRowViewModel row => ForExtension(Path.GetExtension(row.Name)),
            _ => Icon.Document,
        };

    private static Icon ForExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".json"
            or ".toml"
            or ".yaml"
            or ".yml"
            or ".ini"
            or ".cfg"
            or ".xml"
            or ".txt"
            or ".md" => Icon.DocumentText,
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".tga" or ".dds" =>
                Icon.Image,
            ".nif" or ".nifp" or ".kf" or ".kfm" => Icon.Cube,
            ".esp" or ".esm" or ".esl" => Icon.PlugConnected,
            ".wav" or ".ogg" or ".flac" or ".mp3" or ".wma" => Icon.MusicNote1,
            ".pex" or ".psc" or ".h" or ".c" or ".cpp" => Icon.Code,
            ".dll" => Icon.SettingsCogMultiple,
            ".exe" => Icon.Wrench,
            ".daehidden" => Icon.DocumentSplitHintOff,
            _ => Icon.Document,
        };

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
