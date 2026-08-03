namespace Colors.Domain;

/// <summary>
/// Marker used to locate this assembly from tests and DI registration.
/// Holds no behaviour.
/// </summary>
public static class DomainAssembly
{
    public static readonly System.Reflection.Assembly Reference = typeof(DomainAssembly).Assembly;
}
