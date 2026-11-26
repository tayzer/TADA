namespace Tada.RuntimeProbe;

public static class CastleProxy
{
    public const string Kind = "castle-proxy";

    public static bool IsProxy(string identity) =>
        identity.StartsWith("DynamicProxyGenAssembly2,", StringComparison.Ordinal);

    public static bool IsGenerator(string? identity) =>
        identity != null &&
        identity.StartsWith("Castle.Core, Version=", StringComparison.Ordinal) &&
        identity.EndsWith(", Culture=neutral, PublicKeyToken=407dd0808d44fbdc", StringComparison.Ordinal);
}
