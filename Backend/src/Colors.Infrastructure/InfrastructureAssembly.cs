namespace Colors.Infrastructure;

/// <summary>
/// Marker used to locate this assembly from tests and DI registration.
/// Holds no behaviour.
/// </summary>
public static class InfrastructureAssembly
{
    public static readonly System.Reflection.Assembly Reference = typeof(InfrastructureAssembly).Assembly;
}
