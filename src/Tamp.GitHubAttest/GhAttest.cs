namespace Tamp.GitHubAttest;

/// <summary>
/// Top-level facade for the supply-chain provenance verification surface. Wraps two CLIs:
/// <list type="bullet">
///   <item><b>gh attestation</b> (GitHub CLI 2.x) — primary verify path for artifacts attested by GitHub Actions workflows via <c>actions/attest-build-provenance</c>.</item>
///   <item><b>cosign</b> (Sigstore) — generic verify-side surface for off-GHA verification, container-image attestations, and keyless / keyed Sigstore workflows.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// <b>This satellite is verify-side only.</b> Attestation GENERATION happens in CI via the
/// <c>actions/attest-build-provenance</c> GitHub Action — not via the gh CLI. The satellite
/// covers <c>gh attestation {verify, download, trusted-root}</c> and cosign's verify-* verbs,
/// plus a few diagnostic verbs (<c>cosign tree</c>, <c>cosign version</c>, <c>cosign initialize</c>).
/// </para>
/// <para>
/// <b>Pairs with <c>Tamp.Syft</c></b> for the full supply-chain story: SBOM (what's inside) +
/// build provenance (how/where it was built). Both attach to the same artifact in the registry.
/// </para>
/// <para>
/// <b>Tool resolution:</b>
/// <code>
/// [FromPath("gh")]     readonly Tool GhCli = null!;
/// [FromPath("cosign")] readonly Tool CosignTool = null!;
/// </code>
/// </para>
/// </remarks>
public static class GhAttest
{
    // ────────────────────────────────────────────────────────────────────────
    //  gh attestation — primary path for GitHub-produced attestations
    // ────────────────────────────────────────────────────────────────────────

    /// <summary><c>gh attestation verify [subject] {--owner|--repo}</c> — verify an artifact's integrity.</summary>
    public static CommandPlan Verify(Tool ghTool, Action<GhAttestationVerifySettings> configure)
        => Run<GhAttestationVerifySettings>(ghTool, configure);

    /// <summary><c>gh attestation download [subject] {--owner|--repo}</c> — pull attestations to disk for offline verify.</summary>
    public static CommandPlan Download(Tool ghTool, Action<GhAttestationDownloadSettings> configure)
        => Run<GhAttestationDownloadSettings>(ghTool, configure);

    /// <summary><c>gh attestation trusted-root</c> — emit the trusted-root config (for use with offline verification).</summary>
    public static CommandPlan TrustedRoot(Tool ghTool, Action<GhAttestationTrustedRootSettings>? configure = null)
        => Run<GhAttestationTrustedRootSettings>(ghTool, configure);

    // ────────────────────────────────────────────────────────────────────────
    //  Cosign — generic Sigstore verify-side surface
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>Nested verbs under <c>cosign</c> — the generic Sigstore CLI.</summary>
    public static class Cosign
    {
        public static CommandPlan VerifyBlobAttestation(Tool cosignTool, Action<CosignVerifyBlobAttestationSettings> configure)
            => RunCosign<CosignVerifyBlobAttestationSettings>(cosignTool, configure);

        public static CommandPlan VerifyBlob(Tool cosignTool, Action<CosignVerifyBlobSettings> configure)
            => RunCosign<CosignVerifyBlobSettings>(cosignTool, configure);

        public static CommandPlan VerifyAttestation(Tool cosignTool, Action<CosignVerifyAttestationSettings> configure)
            => RunCosign<CosignVerifyAttestationSettings>(cosignTool, configure);

        public static CommandPlan Verify(Tool cosignTool, Action<CosignVerifySettings> configure)
            => RunCosign<CosignVerifySettings>(cosignTool, configure);

        public static CommandPlan Tree(Tool cosignTool, Action<CosignTreeSettings> configure)
            => RunCosign<CosignTreeSettings>(cosignTool, configure);

        public static CommandPlan Version(Tool cosignTool, Action<CosignVersionSettings>? configure = null)
            => RunCosign<CosignVersionSettings>(cosignTool, configure);

        public static CommandPlan Initialize(Tool cosignTool, Action<CosignInitializeSettings>? configure = null)
            => RunCosign<CosignInitializeSettings>(cosignTool, configure);

        public static CommandPlan Raw(Tool cosignTool, params string[] arguments)
        {
            if (cosignTool is null) throw new ArgumentNullException(nameof(cosignTool));
            if (arguments is null || arguments.Length == 0)
                throw new ArgumentException("Raw requires at least one argument.", nameof(arguments));
            return new CommandPlan
            {
                Executable = cosignTool.Executable.Value,
                Arguments = arguments.ToList(),
                Environment = new Dictionary<string, string>(),
                WorkingDirectory = cosignTool.WorkingDirectory,
                Secrets = Array.Empty<Secret>(),
            };
        }

        private static CommandPlan RunCosign<T>(Tool tool, Action<T>? configure) where T : CosignSettingsBase, new()
        {
            if (tool is null) throw new ArgumentNullException(nameof(tool));
            var s = new T();
            configure?.Invoke(s);
            return s.ToCommandPlan(tool);
        }
    }

    /// <summary>Raw escape hatch for <c>gh attestation</c> verbs not yet typed.</summary>
    public static CommandPlan Raw(Tool ghTool, params string[] arguments)
    {
        if (ghTool is null) throw new ArgumentNullException(nameof(ghTool));
        if (arguments is null || arguments.Length == 0)
            throw new ArgumentException("Raw requires at least one argument.", nameof(arguments));
        return new CommandPlan
        {
            Executable = ghTool.Executable.Value,
            Arguments = arguments.ToList(),
            Environment = new Dictionary<string, string>(),
            WorkingDirectory = ghTool.WorkingDirectory,
            Secrets = Array.Empty<Secret>(),
        };
    }

    private static CommandPlan Run<T>(Tool tool, Action<T>? configure) where T : GhAttestationSettingsBase, new()
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        var s = new T();
        configure?.Invoke(s);
        return s.ToCommandPlan(tool);
    }
}
