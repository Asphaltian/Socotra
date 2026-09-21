namespace Microsoft.AspNetCore.Components.CompilerServices;

/// <summary>Your Razor files compile to calls to these, so you won't need to call them yourself.</summary>
public static class RuntimeHelpers
{
    /// <summary>Checks a value's type when your Razor file compiles.</summary>
    public static T TypeCheck<T>(T value) => value;
}
