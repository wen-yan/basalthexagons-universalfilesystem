using System;

namespace BasaltHexagons.UniversalFileSystem.Cli;

/// <summary>
/// This attribute is used to mark static method which is used to configure services.
/// When `Profiles` is empty or it contains any profile, the marked method is invoked.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class ServicesConfiguratorAttribute : Attribute
{
}
