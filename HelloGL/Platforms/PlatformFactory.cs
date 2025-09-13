namespace HelloGL.Platforms;

public static class PlatformFactory
{
    public static IPlatform CreatePlatform(PlatformOptions options)
    {
        if (OperatingSystem.IsWindows())
        {
            return new Windows.WindowsPlatform(options);
        }
        
        if (OperatingSystem.IsLinux())
        {
            var xdgSessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
            if (string.Equals(xdgSessionType, "wayland", StringComparison.OrdinalIgnoreCase))
            {
                return new LinuxWayland.LinuxWaylandPlatform(options);
            }
            else
            {
                return new LinuxX11.LinuxXorgPlatform(options);
            }
        }
        
        throw new PlatformNotSupportedException("Unsupported OS platform.");
    }
}


public record PlatformOptions();
public record WindowOptions(int Width, int Height, string Title, int SwapInterval = 1); // 0=off, 1=vsync



public interface IPlatform : IDisposable
{
    IWindow CreateWindow(WindowOptions options);
}

public interface IWindow : IDisposable
{
    GL Gl { get; }

    (int, int) Size { get; }



    bool ProcessEvents();

    void SwapBuffers();
}