namespace Colors.Application;

/// <summary>
/// Marker used to locate this assembly from tests and DI registration.
/// Holds no behaviour.
/// </summary>
public static class ApplicationAssembly
{
    public static readonly System.Reflection.Assembly Reference = typeof(ApplicationAssembly).Assembly;
}
