using Avalonia.Platform;
using DaisyOS.Shell.Services;
using Xunit;

namespace DaisyOS.Tests;

public sealed class TextShaperCompatibilityPolicyTests
{
    [Theory]
    [InlineData("QEMU")]
    [InlineData("QEMU, Inc.")]
    [InlineData("Standard PC (Q35 + ICH9, 2009) (KVM)")]
    [InlineData("Bochs")]
    public void Known_emulators_enable_the_compatibility_shaper(string productName)
    {
        Assert.True(TextShaperCompatibilityPolicy.IsKnownEmulator(productName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Framework Laptop 13")]
    [InlineData("MacBookPro18,3")]
    public void Physical_hardware_keeps_full_text_shaping(string? productName)
    {
        Assert.False(TextShaperCompatibilityPolicy.IsKnownEmulator(productName));
    }

    [Fact]
    public void Runtime_adapter_satisfies_avalonias_framework_interface()
    {
        var adapter = ManagedTextShaperAdapter.Create();

        Assert.IsAssignableFrom<ITextShaperImpl>(adapter);
        using var typeface = ManagedTextShaperAdapter.CreateTypeface();
        Assert.NotNull(typeface);
    }

    [Fact]
    public void Bundled_material_symbols_expose_direct_glyphs_for_shell_icons()
    {
        var fontPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "DaisyOS.Shell",
            "Assets",
            "Icons",
            "MaterialSymbolsRounded-VariableFont_FILL,GRAD,opsz,wght.ttf");
        var glyphs = ManagedTextShaper.ParsePostGlyphNames(File.ReadAllBytes(fontPath));

        foreach (var icon in new[]
                 {
                     "search", "signal_wifi_4_bar", "bluetooth", "volume_up", "sports_esports",
                     "light_mode", "notifications", "person", "power_settings_new", "folder",
                     "restart_alt", "content_copy", "content_cut", "content_paste", "delete"
                 })
        {
            Assert.True(glyphs.TryGetValue(icon, out var glyph), $"Missing icon glyph: {icon}");
            Assert.NotEqual((ushort)0, glyph);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DaisyOS.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
