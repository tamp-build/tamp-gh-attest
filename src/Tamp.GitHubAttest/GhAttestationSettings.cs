namespace Tamp.GitHubAttest;

/// <summary>Common shape shared by every <c>gh attestation</c> verb.</summary>
public abstract class GhAttestationSettingsBase
{
    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    /// <summary>Override the GitHub host (<c>--hostname</c>) — for GHES.</summary>
    public string? Hostname { get; set; }

    /// <summary>Maximum number of attestations to fetch (<c>--limit</c>, default 30).</summary>
    public int? Limit { get; set; }

    /// <summary>Filter by predicate type (<c>--predicate-type</c>). Default for verify: <c>https://slsa.dev/provenance/v1</c>.</summary>
    public string? PredicateType { get; set; }

    /// <summary>GitHub organization to scope lookup by (<c>-o / --owner</c>). Mutually exclusive with <see cref="Repo"/>.</summary>
    public string? Owner { get; set; }

    /// <summary>Repository in <c>owner/repo</c> form (<c>-R / --repo</c>). Mutually exclusive with <see cref="Owner"/>.</summary>
    public string? Repo { get; set; }

    protected abstract IEnumerable<string> Verb { get; }
    protected abstract void AppendArguments(List<string> args);

    internal CommandPlan ToCommandPlan(Tool tool)
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        if (!string.IsNullOrEmpty(Owner) && !string.IsNullOrEmpty(Repo))
            throw new InvalidOperationException("--owner and --repo are mutually exclusive; pick exactly one.");

        var args = new List<string>(Verb);
        AppendArguments(args);
        if (!string.IsNullOrEmpty(Owner)) { args.Add("-o"); args.Add(Owner!); }
        if (!string.IsNullOrEmpty(Repo)) { args.Add("-R"); args.Add(Repo!); }
        if (!string.IsNullOrEmpty(PredicateType)) { args.Add("--predicate-type"); args.Add(PredicateType!); }
        if (!string.IsNullOrEmpty(Hostname)) { args.Add("--hostname"); args.Add(Hostname!); }
        if (Limit is int l)
        {
            if (l < 1) throw new InvalidOperationException($"Limit must be >= 1; got {l}.");
            args.Add("-L"); args.Add(l.ToString());
        }

        return new CommandPlan
        {
            Executable = tool.Executable.Value,
            Arguments = args,
            Environment = new Dictionary<string, string>(EnvironmentVariables),
            WorkingDirectory = WorkingDirectory ?? tool.WorkingDirectory,
            Secrets = Array.Empty<Secret>(),
        };
    }
}

/// <summary>
/// Settings for <c>gh attestation verify</c> — verify an artifact's integrity using attestations.
/// Default predicate type is <c>https://slsa.dev/provenance/v1</c> (SLSA build provenance).
/// </summary>
public sealed class GhAttestationVerifySettings : GhAttestationSettingsBase
{
    /// <summary>Subject artifact (positional). Local file path, or <c>oci://&lt;image-uri&gt;</c>.</summary>
    public string? Subject { get; set; }

    /// <summary>Local attestation bundle file (<c>--bundle</c>). Use for offline verification.</summary>
    public string? BundlePath { get; set; }

    /// <summary>Enforce signer-repo identity (<c>--signer-repo &lt;owner&gt;/&lt;repo&gt;</c>) — when signed by a reusable workflow.</summary>
    public string? SignerRepo { get; set; }

    /// <summary>Enforce signer-workflow identity (<c>--signer-workflow [host/]&lt;owner&gt;/&lt;repo&gt;/&lt;path&gt;</c>).</summary>
    public string? SignerWorkflow { get; set; }

    /// <summary>Enforce signer-digest (<c>--signer-digest</c>).</summary>
    public string? SignerDigest { get; set; }

    /// <summary>Enforce source-repo digest (<c>--source-digest</c>).</summary>
    public string? SourceDigest { get; set; }

    /// <summary>Enforce source-repo git ref (<c>--source-ref</c>).</summary>
    public string? SourceRef { get; set; }

    /// <summary>Output format (<c>--format json</c>).</summary>
    public string? Format { get; set; }

    /// <summary>jq filter expression (<c>-q / --jq</c>).</summary>
    public string? JqExpression { get; set; }

    /// <summary>Go-template format (<c>-t / --template</c>).</summary>
    public string? TemplatePath { get; set; }

    /// <summary>Disable verification against Sigstore public good instance (<c>--no-public-good</c>) — for private Sigstore deployments.</summary>
    public bool NoPublicGood { get; set; }

    public GhAttestationVerifySettings SetSubject(string subject) { Subject = subject; return this; }
    public GhAttestationVerifySettings SetSubjectFile(string path) { Subject = path; return this; }
    public GhAttestationVerifySettings SetSubjectOciImage(string imageUri) { Subject = imageUri.StartsWith("oci://", StringComparison.Ordinal) ? imageUri : $"oci://{imageUri}"; return this; }
    public GhAttestationVerifySettings SetOwner(string owner) { Owner = owner; return this; }
    public GhAttestationVerifySettings SetRepo(string repo) { Repo = repo; return this; }
    public GhAttestationVerifySettings SetBundle(string bundlePath) { BundlePath = bundlePath; return this; }
    public GhAttestationVerifySettings SetSignerRepo(string ownerSlashRepo) { SignerRepo = ownerSlashRepo; return this; }
    public GhAttestationVerifySettings SetSignerWorkflow(string workflowRef) { SignerWorkflow = workflowRef; return this; }
    public GhAttestationVerifySettings SetSignerDigest(string digest) { SignerDigest = digest; return this; }
    public GhAttestationVerifySettings SetSourceDigest(string digest) { SourceDigest = digest; return this; }
    public GhAttestationVerifySettings SetSourceRef(string gitRef) { SourceRef = gitRef; return this; }
    public GhAttestationVerifySettings SetPredicateType(string predicate) { PredicateType = predicate; return this; }
    public GhAttestationVerifySettings SetFormat(string format) { Format = format; return this; }
    public GhAttestationVerifySettings SetFormatJson() { Format = "json"; return this; }
    public GhAttestationVerifySettings SetJqExpression(string expr) { JqExpression = expr; return this; }
    public GhAttestationVerifySettings SetTemplatePath(string path) { TemplatePath = path; return this; }
    public GhAttestationVerifySettings SetNoPublicGood(bool v = true) { NoPublicGood = v; return this; }
    public GhAttestationVerifySettings SetHostname(string host) { Hostname = host; return this; }
    public GhAttestationVerifySettings SetLimit(int n) { Limit = n; return this; }
    public GhAttestationVerifySettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }
    public GhAttestationVerifySettings SetEnvironmentVariable(string name, string value) { EnvironmentVariables[name] = value; return this; }

    protected override IEnumerable<string> Verb => new[] { "attestation", "verify" };

    protected override void AppendArguments(List<string> args)
    {
        if (string.IsNullOrEmpty(Subject))
            throw new InvalidOperationException(
                "Subject is required for `gh attestation verify` — set via SetSubjectFile(path) or SetSubjectOciImage(image).");
        if (string.IsNullOrEmpty(Owner) && string.IsNullOrEmpty(Repo))
            throw new InvalidOperationException(
                "Either Owner (--owner) or Repo (--repo) is required for `gh attestation verify`.");
        if (!string.IsNullOrEmpty(Format) && Format is not ("json"))
            throw new InvalidOperationException($"Format must be 'json' (the only supported value); got '{Format}'.");

        // Positional subject comes first per `gh attestation verify [subject] [flags]`.
        args.Add(Subject!);
        if (!string.IsNullOrEmpty(BundlePath)) { args.Add("--bundle"); args.Add(BundlePath!); }
        if (!string.IsNullOrEmpty(SignerRepo)) { args.Add("--signer-repo"); args.Add(SignerRepo!); }
        if (!string.IsNullOrEmpty(SignerWorkflow)) { args.Add("--signer-workflow"); args.Add(SignerWorkflow!); }
        if (!string.IsNullOrEmpty(SignerDigest)) { args.Add("--signer-digest"); args.Add(SignerDigest!); }
        if (!string.IsNullOrEmpty(SourceDigest)) { args.Add("--source-digest"); args.Add(SourceDigest!); }
        if (!string.IsNullOrEmpty(SourceRef)) { args.Add("--source-ref"); args.Add(SourceRef!); }
        if (!string.IsNullOrEmpty(Format)) { args.Add("--format"); args.Add(Format!); }
        if (!string.IsNullOrEmpty(JqExpression)) { args.Add("-q"); args.Add(JqExpression!); }
        if (!string.IsNullOrEmpty(TemplatePath)) { args.Add("-t"); args.Add(TemplatePath!); }
        if (NoPublicGood) args.Add("--no-public-good");
    }
}

/// <summary>Settings for <c>gh attestation download</c> — pull attestations to disk for offline verification.</summary>
public sealed class GhAttestationDownloadSettings : GhAttestationSettingsBase
{
    /// <summary>Subject artifact (positional).</summary>
    public string? Subject { get; set; }

    /// <summary>Digest algorithm (<c>-d / --digest-alg</c>): <c>sha256</c> (default) or <c>sha512</c>.</summary>
    public string? DigestAlgorithm { get; set; }

    public GhAttestationDownloadSettings SetSubjectFile(string path) { Subject = path; return this; }
    public GhAttestationDownloadSettings SetSubjectOciImage(string imageUri) { Subject = imageUri.StartsWith("oci://", StringComparison.Ordinal) ? imageUri : $"oci://{imageUri}"; return this; }
    public GhAttestationDownloadSettings SetOwner(string owner) { Owner = owner; return this; }
    public GhAttestationDownloadSettings SetRepo(string repo) { Repo = repo; return this; }
    public GhAttestationDownloadSettings SetDigestAlgorithm(string algo) { DigestAlgorithm = algo; return this; }
    public GhAttestationDownloadSettings SetPredicateType(string predicate) { PredicateType = predicate; return this; }
    public GhAttestationDownloadSettings SetHostname(string host) { Hostname = host; return this; }
    public GhAttestationDownloadSettings SetLimit(int n) { Limit = n; return this; }
    public GhAttestationDownloadSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }

    protected override IEnumerable<string> Verb => new[] { "attestation", "download" };

    protected override void AppendArguments(List<string> args)
    {
        if (string.IsNullOrEmpty(Subject))
            throw new InvalidOperationException(
                "Subject is required for `gh attestation download` — set via SetSubjectFile(path) or SetSubjectOciImage(image).");
        if (string.IsNullOrEmpty(Owner) && string.IsNullOrEmpty(Repo))
            throw new InvalidOperationException(
                "Either Owner (--owner) or Repo (--repo) is required for `gh attestation download`.");
        if (!string.IsNullOrEmpty(DigestAlgorithm) && DigestAlgorithm is not ("sha256" or "sha512"))
            throw new InvalidOperationException(
                $"DigestAlgorithm must be 'sha256' or 'sha512'; got '{DigestAlgorithm}'.");

        args.Add(Subject!);
        if (!string.IsNullOrEmpty(DigestAlgorithm)) { args.Add("-d"); args.Add(DigestAlgorithm!); }
    }
}

/// <summary>Settings for <c>gh attestation trusted-root</c> — emit the trusted-root config for offline verification.</summary>
public sealed class GhAttestationTrustedRootSettings : GhAttestationSettingsBase
{
    public GhAttestationTrustedRootSettings SetHostname(string host) { Hostname = host; return this; }
    public GhAttestationTrustedRootSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }
    protected override IEnumerable<string> Verb => new[] { "attestation", "trusted-root" };
    protected override void AppendArguments(List<string> args) { }
}
