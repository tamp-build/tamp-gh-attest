namespace Tamp.GitHubAttest;

/// <summary>Common shape shared by every <c>cosign</c> verify-side verb.</summary>
public abstract class CosignSettingsBase
{
    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    /// <summary>Verbose output (<c>-d / --verbose</c>).</summary>
    public bool Verbose { get; set; }

    protected abstract IEnumerable<string> Verb { get; }
    protected abstract void AppendArguments(List<string> args);
    protected virtual IReadOnlyList<Secret> CollectSecrets() => Array.Empty<Secret>();

    internal CommandPlan ToCommandPlan(Tool tool)
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        var args = new List<string>(Verb);
        AppendArguments(args);
        if (Verbose) args.Add("-d");
        return new CommandPlan
        {
            Executable = tool.Executable.Value,
            Arguments = args,
            Environment = new Dictionary<string, string>(EnvironmentVariables),
            WorkingDirectory = WorkingDirectory ?? tool.WorkingDirectory,
            Secrets = CollectSecrets(),
        };
    }
}

/// <summary>
/// Settings for <c>cosign verify-blob-attestation</c> — verify an attestation on a local blob.
/// Used for the off-GHA verify path (when you've downloaded attestations via <c>gh attestation download</c>
/// or stored them out-of-band and want to verify without the gh CLI).
/// </summary>
public sealed class CosignVerifyBlobAttestationSettings : CosignSettingsBase
{
    /// <summary>Path to the blob being attested (positional).</summary>
    public string? BlobPath { get; set; }

    /// <summary>Public key file or KMS URI (<c>--key</c>).</summary>
    public string? KeyRef { get; set; }

    /// <summary>Signature path or base64 (<c>--signature</c>).</summary>
    public string? SignatureRef { get; set; }

    /// <summary>Bundle file path (<c>--bundle</c>).</summary>
    public string? BundlePath { get; set; }

    /// <summary>Certificate identity to match (<c>--certificate-identity</c>) — keyless verification.</summary>
    public string? CertificateIdentity { get; set; }

    /// <summary>Certificate identity regex (<c>--certificate-identity-regexp</c>).</summary>
    public string? CertificateIdentityRegexp { get; set; }

    /// <summary>OIDC issuer (<c>--certificate-oidc-issuer</c>).</summary>
    public string? CertificateOidcIssuer { get; set; }

    /// <summary>OIDC issuer regex (<c>--certificate-oidc-issuer-regexp</c>).</summary>
    public string? CertificateOidcIssuerRegexp { get; set; }

    /// <summary>Path to public certificate file (<c>--certificate</c>).</summary>
    public string? CertificatePath { get; set; }

    /// <summary>Path to CA roots bundle (<c>--ca-roots</c>).</summary>
    public string? CaRoots { get; set; }

    /// <summary>Path to CA intermediates bundle (<c>--ca-intermediates</c>).</summary>
    public string? CaIntermediates { get; set; }

    /// <summary>Predicate-type filter (<c>--type</c>). Default behavior: any type accepted.</summary>
    public string? PredicateType { get; set; }

    public CosignVerifyBlobAttestationSettings SetBlobPath(string path) { BlobPath = path; return this; }
    public CosignVerifyBlobAttestationSettings SetKey(string keyRef) { KeyRef = keyRef; return this; }
    public CosignVerifyBlobAttestationSettings SetSignature(string sigRef) { SignatureRef = sigRef; return this; }
    public CosignVerifyBlobAttestationSettings SetBundle(string path) { BundlePath = path; return this; }
    public CosignVerifyBlobAttestationSettings SetCertificateIdentity(string identity) { CertificateIdentity = identity; return this; }
    public CosignVerifyBlobAttestationSettings SetCertificateIdentityRegexp(string regex) { CertificateIdentityRegexp = regex; return this; }
    public CosignVerifyBlobAttestationSettings SetCertificateOidcIssuer(string issuer) { CertificateOidcIssuer = issuer; return this; }
    public CosignVerifyBlobAttestationSettings SetCertificateOidcIssuerRegexp(string regex) { CertificateOidcIssuerRegexp = regex; return this; }
    public CosignVerifyBlobAttestationSettings SetCertificate(string path) { CertificatePath = path; return this; }
    public CosignVerifyBlobAttestationSettings SetCaRoots(string path) { CaRoots = path; return this; }
    public CosignVerifyBlobAttestationSettings SetCaIntermediates(string path) { CaIntermediates = path; return this; }
    public CosignVerifyBlobAttestationSettings SetPredicateType(string predicate) { PredicateType = predicate; return this; }
    public CosignVerifyBlobAttestationSettings SetVerbose(bool v = true) { Verbose = v; return this; }
    public CosignVerifyBlobAttestationSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }
    public CosignVerifyBlobAttestationSettings SetEnvironmentVariable(string name, string value) { EnvironmentVariables[name] = value; return this; }

    protected override IEnumerable<string> Verb => new[] { "verify-blob-attestation" };

    protected override void AppendArguments(List<string> args)
    {
        if (string.IsNullOrEmpty(BlobPath))
            throw new InvalidOperationException(
                "BlobPath is required for `cosign verify-blob-attestation` — set via SetBlobPath(path).");
        // Verification requires *some* identity: either a key, a bundle (with identity baked in),
        // or keyless identity (cert-identity + oidc-issuer).
        var hasKey = !string.IsNullOrEmpty(KeyRef);
        var hasBundle = !string.IsNullOrEmpty(BundlePath);
        var hasKeyless = !string.IsNullOrEmpty(CertificateIdentity) || !string.IsNullOrEmpty(CertificateIdentityRegexp);
        if (!hasKey && !hasBundle && !hasKeyless)
            throw new InvalidOperationException(
                "Verification needs an identity selector — set one of: SetKey(...), SetBundle(...), or SetCertificateIdentity(...)/SetCertificateIdentityRegexp(...) (+ matching OIDC issuer for keyless).");
        if (!string.IsNullOrEmpty(CertificateIdentity) && !string.IsNullOrEmpty(CertificateIdentityRegexp))
            throw new InvalidOperationException(
                "CertificateIdentity and CertificateIdentityRegexp are mutually exclusive — pick one.");
        if (!string.IsNullOrEmpty(CertificateOidcIssuer) && !string.IsNullOrEmpty(CertificateOidcIssuerRegexp))
            throw new InvalidOperationException(
                "CertificateOidcIssuer and CertificateOidcIssuerRegexp are mutually exclusive — pick one.");
        if (hasKeyless && string.IsNullOrEmpty(CertificateOidcIssuer) && string.IsNullOrEmpty(CertificateOidcIssuerRegexp))
            throw new InvalidOperationException(
                "Keyless verification requires --certificate-oidc-issuer (or its -regexp variant) — set via SetCertificateOidcIssuer(...).");
        if (!string.IsNullOrEmpty(CaRoots) && !string.IsNullOrEmpty(CertificatePath))
            throw new InvalidOperationException(
                "CaRoots and Certificate are mutually exclusive flag families — pick the trust model that applies.");

        if (!string.IsNullOrEmpty(KeyRef)) { args.Add("--key"); args.Add(KeyRef!); }
        if (!string.IsNullOrEmpty(SignatureRef)) { args.Add("--signature"); args.Add(SignatureRef!); }
        if (!string.IsNullOrEmpty(BundlePath)) { args.Add("--bundle"); args.Add(BundlePath!); }
        if (!string.IsNullOrEmpty(CertificatePath)) { args.Add("--certificate"); args.Add(CertificatePath!); }
        if (!string.IsNullOrEmpty(CertificateIdentity)) { args.Add("--certificate-identity"); args.Add(CertificateIdentity!); }
        if (!string.IsNullOrEmpty(CertificateIdentityRegexp)) { args.Add("--certificate-identity-regexp"); args.Add(CertificateIdentityRegexp!); }
        if (!string.IsNullOrEmpty(CertificateOidcIssuer)) { args.Add("--certificate-oidc-issuer"); args.Add(CertificateOidcIssuer!); }
        if (!string.IsNullOrEmpty(CertificateOidcIssuerRegexp)) { args.Add("--certificate-oidc-issuer-regexp"); args.Add(CertificateOidcIssuerRegexp!); }
        if (!string.IsNullOrEmpty(CaRoots)) { args.Add("--ca-roots"); args.Add(CaRoots!); }
        if (!string.IsNullOrEmpty(CaIntermediates)) { args.Add("--ca-intermediates"); args.Add(CaIntermediates!); }
        if (!string.IsNullOrEmpty(PredicateType)) { args.Add("--type"); args.Add(PredicateType!); }

        args.Add(BlobPath!);
    }
}

/// <summary>Settings for <c>cosign verify-blob</c> — verify a blob's signature (no attestation predicate).</summary>
public sealed class CosignVerifyBlobSettings : CosignSettingsBase
{
    public string? BlobPath { get; set; }
    public string? KeyRef { get; set; }
    public string? SignatureRef { get; set; }
    public string? BundlePath { get; set; }
    public string? CertificateIdentity { get; set; }
    public string? CertificateOidcIssuer { get; set; }

    public CosignVerifyBlobSettings SetBlobPath(string path) { BlobPath = path; return this; }
    public CosignVerifyBlobSettings SetKey(string keyRef) { KeyRef = keyRef; return this; }
    public CosignVerifyBlobSettings SetSignature(string sigRef) { SignatureRef = sigRef; return this; }
    public CosignVerifyBlobSettings SetBundle(string path) { BundlePath = path; return this; }
    public CosignVerifyBlobSettings SetCertificateIdentity(string identity) { CertificateIdentity = identity; return this; }
    public CosignVerifyBlobSettings SetCertificateOidcIssuer(string issuer) { CertificateOidcIssuer = issuer; return this; }
    public CosignVerifyBlobSettings SetVerbose(bool v = true) { Verbose = v; return this; }
    public CosignVerifyBlobSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override IEnumerable<string> Verb => new[] { "verify-blob" };

    protected override void AppendArguments(List<string> args)
    {
        if (string.IsNullOrEmpty(BlobPath))
            throw new InvalidOperationException("BlobPath is required for `cosign verify-blob`.");
        var hasKey = !string.IsNullOrEmpty(KeyRef);
        var hasBundle = !string.IsNullOrEmpty(BundlePath);
        var hasKeyless = !string.IsNullOrEmpty(CertificateIdentity);
        if (!hasKey && !hasBundle && !hasKeyless)
            throw new InvalidOperationException(
                "Verification needs an identity selector — set one of: SetKey(...), SetBundle(...), or SetCertificateIdentity(...) + SetCertificateOidcIssuer(...).");
        if (hasKeyless && string.IsNullOrEmpty(CertificateOidcIssuer))
            throw new InvalidOperationException(
                "Keyless verification requires SetCertificateOidcIssuer(...).");

        if (!string.IsNullOrEmpty(KeyRef)) { args.Add("--key"); args.Add(KeyRef!); }
        if (!string.IsNullOrEmpty(SignatureRef)) { args.Add("--signature"); args.Add(SignatureRef!); }
        if (!string.IsNullOrEmpty(BundlePath)) { args.Add("--bundle"); args.Add(BundlePath!); }
        if (!string.IsNullOrEmpty(CertificateIdentity)) { args.Add("--certificate-identity"); args.Add(CertificateIdentity!); }
        if (!string.IsNullOrEmpty(CertificateOidcIssuer)) { args.Add("--certificate-oidc-issuer"); args.Add(CertificateOidcIssuer!); }
        args.Add(BlobPath!);
    }
}

/// <summary>Settings for <c>cosign verify-attestation</c> — verify an attestation on a container image.</summary>
public sealed class CosignVerifyAttestationSettings : CosignSettingsBase
{
    public string? ImageRef { get; set; }
    public string? KeyRef { get; set; }
    public string? CertificateIdentity { get; set; }
    public string? CertificateOidcIssuer { get; set; }
    public string? PredicateType { get; set; }

    public CosignVerifyAttestationSettings SetImage(string image) { ImageRef = image; return this; }
    public CosignVerifyAttestationSettings SetKey(string keyRef) { KeyRef = keyRef; return this; }
    public CosignVerifyAttestationSettings SetCertificateIdentity(string identity) { CertificateIdentity = identity; return this; }
    public CosignVerifyAttestationSettings SetCertificateOidcIssuer(string issuer) { CertificateOidcIssuer = issuer; return this; }
    public CosignVerifyAttestationSettings SetPredicateType(string predicate) { PredicateType = predicate; return this; }
    public CosignVerifyAttestationSettings SetVerbose(bool v = true) { Verbose = v; return this; }
    public CosignVerifyAttestationSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override IEnumerable<string> Verb => new[] { "verify-attestation" };

    protected override void AppendArguments(List<string> args)
    {
        if (string.IsNullOrEmpty(ImageRef))
            throw new InvalidOperationException("ImageRef is required for `cosign verify-attestation`.");
        var hasKey = !string.IsNullOrEmpty(KeyRef);
        var hasKeyless = !string.IsNullOrEmpty(CertificateIdentity);
        if (!hasKey && !hasKeyless)
            throw new InvalidOperationException(
                "Verification needs an identity selector — set one of: SetKey(...) or SetCertificateIdentity(...) + SetCertificateOidcIssuer(...).");
        if (hasKeyless && string.IsNullOrEmpty(CertificateOidcIssuer))
            throw new InvalidOperationException("Keyless verification requires SetCertificateOidcIssuer(...).");

        if (!string.IsNullOrEmpty(KeyRef)) { args.Add("--key"); args.Add(KeyRef!); }
        if (!string.IsNullOrEmpty(CertificateIdentity)) { args.Add("--certificate-identity"); args.Add(CertificateIdentity!); }
        if (!string.IsNullOrEmpty(CertificateOidcIssuer)) { args.Add("--certificate-oidc-issuer"); args.Add(CertificateOidcIssuer!); }
        if (!string.IsNullOrEmpty(PredicateType)) { args.Add("--type"); args.Add(PredicateType!); }
        args.Add(ImageRef!);
    }
}

/// <summary>Settings for <c>cosign verify</c> — verify a signature on a container image.</summary>
public sealed class CosignVerifySettings : CosignSettingsBase
{
    public string? ImageRef { get; set; }
    public string? KeyRef { get; set; }
    public string? CertificateIdentity { get; set; }
    public string? CertificateOidcIssuer { get; set; }

    public CosignVerifySettings SetImage(string image) { ImageRef = image; return this; }
    public CosignVerifySettings SetKey(string keyRef) { KeyRef = keyRef; return this; }
    public CosignVerifySettings SetCertificateIdentity(string identity) { CertificateIdentity = identity; return this; }
    public CosignVerifySettings SetCertificateOidcIssuer(string issuer) { CertificateOidcIssuer = issuer; return this; }
    public CosignVerifySettings SetVerbose(bool v = true) { Verbose = v; return this; }
    public CosignVerifySettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override IEnumerable<string> Verb => new[] { "verify" };

    protected override void AppendArguments(List<string> args)
    {
        if (string.IsNullOrEmpty(ImageRef))
            throw new InvalidOperationException("ImageRef is required for `cosign verify`.");
        var hasKey = !string.IsNullOrEmpty(KeyRef);
        var hasKeyless = !string.IsNullOrEmpty(CertificateIdentity);
        if (!hasKey && !hasKeyless)
            throw new InvalidOperationException(
                "Verification needs an identity selector — set one of: SetKey(...) or SetCertificateIdentity(...) + SetCertificateOidcIssuer(...).");
        if (hasKeyless && string.IsNullOrEmpty(CertificateOidcIssuer))
            throw new InvalidOperationException("Keyless verification requires SetCertificateOidcIssuer(...).");

        if (!string.IsNullOrEmpty(KeyRef)) { args.Add("--key"); args.Add(KeyRef!); }
        if (!string.IsNullOrEmpty(CertificateIdentity)) { args.Add("--certificate-identity"); args.Add(CertificateIdentity!); }
        if (!string.IsNullOrEmpty(CertificateOidcIssuer)) { args.Add("--certificate-oidc-issuer"); args.Add(CertificateOidcIssuer!); }
        args.Add(ImageRef!);
    }
}

/// <summary>Settings for <c>cosign tree</c> — show supply-chain artifacts (signatures, SBOMs, attestations) for an image.</summary>
public sealed class CosignTreeSettings : CosignSettingsBase
{
    public string? ImageRef { get; set; }
    public CosignTreeSettings SetImage(string image) { ImageRef = image; return this; }
    public CosignTreeSettings SetVerbose(bool v = true) { Verbose = v; return this; }
    public CosignTreeSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }
    protected override IEnumerable<string> Verb => new[] { "tree" };
    protected override void AppendArguments(List<string> args)
    {
        if (string.IsNullOrEmpty(ImageRef))
            throw new InvalidOperationException("ImageRef is required for `cosign tree`.");
        args.Add(ImageRef!);
    }
}

/// <summary>Settings for <c>cosign version</c> — diagnostic.</summary>
public sealed class CosignVersionSettings : CosignSettingsBase
{
    public CosignVersionSettings SetVerbose(bool v = true) { Verbose = v; return this; }
    public CosignVersionSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }
    protected override IEnumerable<string> Verb => new[] { "version" };
    protected override void AppendArguments(List<string> args) { }
}

/// <summary>Settings for <c>cosign initialize</c> — initialize / refresh the Sigstore TUF trust root.</summary>
public sealed class CosignInitializeSettings : CosignSettingsBase
{
    /// <summary>TUF mirror URL (<c>--mirror</c>).</summary>
    public string? Mirror { get; set; }
    /// <summary>Path to the trusted root (<c>--root</c>).</summary>
    public string? Root { get; set; }

    public CosignInitializeSettings SetMirror(string url) { Mirror = url; return this; }
    public CosignInitializeSettings SetRoot(string path) { Root = path; return this; }
    public CosignInitializeSettings SetVerbose(bool v = true) { Verbose = v; return this; }
    public CosignInitializeSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override IEnumerable<string> Verb => new[] { "initialize" };
    protected override void AppendArguments(List<string> args)
    {
        if (!string.IsNullOrEmpty(Mirror)) { args.Add("--mirror"); args.Add(Mirror!); }
        if (!string.IsNullOrEmpty(Root)) { args.Add("--root"); args.Add(Root!); }
    }
}
