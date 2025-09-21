#if NETSTANDARD1_0_OR_GREATER || NET6_0
// ReSharper disable once CheckNamespace
namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Shim for <c>SetsRequiredMembersAttribute</c> used when targeting frameworks that do not
/// expose the attribute. Indicates that invoking the annotated constructor sets all
/// required members for the constructed type.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class SetsRequiredMembersAttribute : Attribute
{
}

/// <summary>
/// Shim for <c>RequiresUnreferencedCodeAttribute</c>; applied to APIs that are incompatible
/// with trimming or require dynamic code preservation.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
#if SYSTEM_PRIVATE_CORELIB
    public
#else
internal
#endif
    sealed class RequiresUnreferencedCodeAttribute(string message) : Attribute
{
    /// <summary>Explanatory message describing why the annotated member is unsafe for trimming.</summary>
    public string Message { get; } = message;
}

#endif