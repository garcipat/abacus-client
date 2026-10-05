namespace Garcipat.AbacusApi.Tests.Infrastructure;

/// <summary>A fact that only runs on Windows (e.g. DPAPI).</summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Windows only.";
    }
}
