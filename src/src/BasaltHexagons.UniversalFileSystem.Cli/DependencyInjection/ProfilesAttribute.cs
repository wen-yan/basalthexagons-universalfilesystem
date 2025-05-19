using System;

namespace BasaltHexagons.UniversalFileSystem.Cli.DependencyInjection;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class ProfilesAttribute : Attribute
{
    public ProfilesAttribute(params string[] profiles)
    {
        this.Profiles = profiles;
    }
    public string[] Profiles { get; }
}