#if NETSTANDARD1_0_OR_GREATER || NET6_0
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit
{
}

/// <summary>
/// Shim for the C# 11 <c>RequiredMemberAttribute</c>, applied to members that must be
/// initialized during object construction (usually in object initializers or by constructors).
/// Present only for target frameworks that lack the built-in attribute.
/// </summary>
public class RequiredMemberAttribute : Attribute { }

/// <summary>
/// Shim for <c>CompilerFeatureRequiredAttribute</c>, used by the compiler to detect presence
/// of specific language features. Provided to avoid compile-time errors on older frameworks.
/// </summary>
public class CompilerFeatureRequiredAttribute : Attribute
{
    /// <summary>Create the attribute with the required feature name.</summary>
    /// <param name="name">Name of the compiler feature.</param>
    public CompilerFeatureRequiredAttribute(string name) { }
}
#endif