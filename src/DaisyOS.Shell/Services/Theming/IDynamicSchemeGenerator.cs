using Avalonia.Media;

namespace DaisyOS.Shell.Services.Theming;

public interface IDynamicSchemeGenerator
{
    DynamicColorScheme Generate(Color seed, bool isDark);
}
