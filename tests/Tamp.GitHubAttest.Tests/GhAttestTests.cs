using System;
using System.Collections.Generic;
using System.Linq;
using Tamp;
using Tamp.GitHubAttest;
using Xunit;

namespace Tamp.GitHubAttest.Tests;

public sealed class GhAttestTests
{
    private static Tool FakeGh() => new(AbsolutePath.Create("/fake/gh"));
    private static Tool FakeCosign() => new(AbsolutePath.Create("/fake/cosign"));

    private static int IndexOf(IReadOnlyList<string> args, string token)
    {
        for (var i = 0; i < args.Count; i++) if (args[i] == token) return i;
        return -1;
    }

    // ─── gh attestation verify ────────────────────────────────────────────

    [Fact]
    public void Verify_Local_Subject_With_Owner()
    {
        var plan = GhAttest.Verify(FakeGh(), s => s
            .SetSubjectFile("DasBook.msix")
            .SetOwner("brewingcoder"));
        Assert.Equal("attestation", plan.Arguments[0]);
        Assert.Equal("verify", plan.Arguments[1]);
        Assert.Equal("DasBook.msix", plan.Arguments[2]);
        Assert.Equal("brewingcoder", plan.Arguments[IndexOf(plan.Arguments, "-o") + 1]);
    }

    [Fact]
    public void Verify_Local_Subject_With_Repo()
    {
        var plan = GhAttest.Verify(FakeGh(), s => s
            .SetSubjectFile("artifact.zip")
            .SetRepo("BrewingCoder/dasbook"));
        Assert.Equal("BrewingCoder/dasbook", plan.Arguments[IndexOf(plan.Arguments, "-R") + 1]);
    }

    [Fact]
    public void Verify_Oci_Image_Subject()
    {
        var plan = GhAttest.Verify(FakeGh(), s => s
            .SetSubjectOciImage("ghcr.io/brewingcoder/dasbook:1.0.6")
            .SetOwner("brewingcoder"));
        Assert.Equal("oci://ghcr.io/brewingcoder/dasbook:1.0.6", plan.Arguments[2]);
    }

    [Fact]
    public void Verify_Already_Prefixed_Oci_Subject_Not_Double_Prefixed()
    {
        var plan = GhAttest.Verify(FakeGh(), s => s
            .SetSubjectOciImage("oci://ghcr.io/x/y:1.0")
            .SetOwner("x"));
        Assert.Equal("oci://ghcr.io/x/y:1.0", plan.Arguments[2]);
    }

    [Fact]
    public void Verify_Owner_And_Repo_Mutually_Exclusive()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GhAttest.Verify(FakeGh(), s => s
                .SetSubjectFile("x.zip").SetOwner("o").SetRepo("o/r")).Arguments.ToList());
    }

    [Fact]
    public void Verify_Subject_Required()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GhAttest.Verify(FakeGh(), s => s.SetOwner("o")).Arguments.ToList());
    }

    [Fact]
    public void Verify_Owner_Or_Repo_Required()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GhAttest.Verify(FakeGh(), s => s.SetSubjectFile("x.zip")).Arguments.ToList());
    }

    [Fact]
    public void Verify_Bundle_For_Offline()
    {
        var plan = GhAttest.Verify(FakeGh(), s => s
            .SetSubjectFile("x.zip").SetOwner("o").SetBundle("./bundle.jsonl"));
        Assert.Equal("./bundle.jsonl", plan.Arguments[IndexOf(plan.Arguments, "--bundle") + 1]);
    }

    [Fact]
    public void Verify_Signer_Workflow_Enforcement()
    {
        var plan = GhAttest.Verify(FakeGh(), s => s
            .SetSubjectFile("x.zip").SetOwner("o")
            .SetSignerRepo("actions/example")
            .SetSignerWorkflow("actions/example/.github/workflows/release.yml")
            .SetSignerDigest("abc123")
            .SetSourceRef("refs/tags/v1.0.6")
            .SetSourceDigest("def456"));
        Assert.Equal("actions/example", plan.Arguments[IndexOf(plan.Arguments, "--signer-repo") + 1]);
        Assert.Equal("actions/example/.github/workflows/release.yml",
            plan.Arguments[IndexOf(plan.Arguments, "--signer-workflow") + 1]);
        Assert.Equal("abc123", plan.Arguments[IndexOf(plan.Arguments, "--signer-digest") + 1]);
        Assert.Equal("refs/tags/v1.0.6", plan.Arguments[IndexOf(plan.Arguments, "--source-ref") + 1]);
        Assert.Equal("def456", plan.Arguments[IndexOf(plan.Arguments, "--source-digest") + 1]);
    }

    [Fact]
    public void Verify_Custom_Predicate_Type()
    {
        var plan = GhAttest.Verify(FakeGh(), s => s
            .SetSubjectFile("x.zip").SetOwner("o")
            .SetPredicateType("https://spdx.dev/Document"));
        Assert.Equal("https://spdx.dev/Document",
            plan.Arguments[IndexOf(plan.Arguments, "--predicate-type") + 1]);
    }

    [Fact]
    public void Verify_Json_Output_Format()
    {
        var plan = GhAttest.Verify(FakeGh(), s => s
            .SetSubjectFile("x.zip").SetOwner("o").SetFormatJson());
        Assert.Equal("json", plan.Arguments[IndexOf(plan.Arguments, "--format") + 1]);
    }

    [Fact]
    public void Verify_Rejects_Non_Json_Format()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GhAttest.Verify(FakeGh(), s => s
                .SetSubjectFile("x.zip").SetOwner("o").SetFormat("xml")).Arguments.ToList());
    }

    [Fact]
    public void Verify_JqExpression_Pass_Through()
    {
        var plan = GhAttest.Verify(FakeGh(), s => s
            .SetSubjectFile("x.zip").SetOwner("o").SetJqExpression(".[].verificationResult.signature"));
        Assert.Equal(".[].verificationResult.signature",
            plan.Arguments[IndexOf(plan.Arguments, "-q") + 1]);
    }

    [Fact]
    public void Verify_NoPublicGood_For_Private_Sigstore()
    {
        var plan = GhAttest.Verify(FakeGh(), s => s
            .SetSubjectFile("x.zip").SetOwner("o").SetNoPublicGood());
        Assert.Contains("--no-public-good", plan.Arguments);
    }

    [Fact]
    public void Verify_Limit_Pass_Through()
    {
        var plan = GhAttest.Verify(FakeGh(), s => s
            .SetSubjectFile("x.zip").SetOwner("o").SetLimit(100));
        Assert.Equal("100", plan.Arguments[IndexOf(plan.Arguments, "-L") + 1]);
    }

    [Fact]
    public void Verify_Limit_Rejects_Zero_Or_Negative()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GhAttest.Verify(FakeGh(), s => s
                .SetSubjectFile("x.zip").SetOwner("o").SetLimit(0)).Arguments.ToList());
    }

    [Fact]
    public void Verify_Hostname_For_GHES()
    {
        var plan = GhAttest.Verify(FakeGh(), s => s
            .SetSubjectFile("x.zip").SetOwner("o").SetHostname("ghe.example.com"));
        Assert.Equal("ghe.example.com", plan.Arguments[IndexOf(plan.Arguments, "--hostname") + 1]);
    }

    // ─── gh attestation download ──────────────────────────────────────────

    [Fact]
    public void Download_Local_Subject()
    {
        var plan = GhAttest.Download(FakeGh(), s => s
            .SetSubjectFile("DasBook.msix").SetOwner("brewingcoder"));
        Assert.Equal(new[] { "attestation", "download", "DasBook.msix" }, plan.Arguments.Take(3));
    }

    [Fact]
    public void Download_Digest_Algorithm_Sha512()
    {
        var plan = GhAttest.Download(FakeGh(), s => s
            .SetSubjectFile("x.zip").SetOwner("o").SetDigestAlgorithm("sha512"));
        Assert.Equal("sha512", plan.Arguments[IndexOf(plan.Arguments, "-d") + 1]);
    }

    [Fact]
    public void Download_Digest_Algorithm_Rejects_Unknown()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GhAttest.Download(FakeGh(), s => s
                .SetSubjectFile("x.zip").SetOwner("o").SetDigestAlgorithm("md5")).Arguments.ToList());
    }

    [Fact]
    public void Download_Subject_Required()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GhAttest.Download(FakeGh(), s => s.SetOwner("o")).Arguments.ToList());
    }

    [Fact]
    public void Download_Owner_Or_Repo_Required()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GhAttest.Download(FakeGh(), s => s.SetSubjectFile("x.zip")).Arguments.ToList());
    }

    // ─── gh attestation trusted-root ──────────────────────────────────────

    [Fact]
    public void TrustedRoot_Verb_Shape()
    {
        var plan = GhAttest.TrustedRoot(FakeGh());
        Assert.Equal(new[] { "attestation", "trusted-root" }, plan.Arguments);
    }

    [Fact]
    public void TrustedRoot_With_Hostname()
    {
        var plan = GhAttest.TrustedRoot(FakeGh(), s => s.SetHostname("ghe.example.com"));
        Assert.Equal("ghe.example.com", plan.Arguments[IndexOf(plan.Arguments, "--hostname") + 1]);
    }

    // ─── cosign verify-blob-attestation ───────────────────────────────────

    [Fact]
    public void Cosign_VerifyBlobAttestation_With_Key()
    {
        var plan = GhAttest.Cosign.VerifyBlobAttestation(FakeCosign(), s => s
            .SetBlobPath("./DasBook.msix")
            .SetKey("cosign.pub")
            .SetSignature("./attest.sig"));
        Assert.Equal("verify-blob-attestation", plan.Arguments[0]);
        Assert.Equal("cosign.pub", plan.Arguments[IndexOf(plan.Arguments, "--key") + 1]);
        Assert.Equal("./attest.sig", plan.Arguments[IndexOf(plan.Arguments, "--signature") + 1]);
        Assert.Equal("./DasBook.msix", plan.Arguments[^1]);
    }

    [Fact]
    public void Cosign_VerifyBlobAttestation_Keyless_Requires_OIDC_Issuer()
    {
        // Keyless verify needs both identity and OIDC issuer; missing the issuer trips validation.
        Assert.Throws<InvalidOperationException>(() =>
            GhAttest.Cosign.VerifyBlobAttestation(FakeCosign(), s => s
                .SetBlobPath("x")
                .SetCertificateIdentity("scott@gscottsingleton.com")).Arguments.ToList());
    }

    [Fact]
    public void Cosign_VerifyBlobAttestation_Keyless_Full_Path()
    {
        var plan = GhAttest.Cosign.VerifyBlobAttestation(FakeCosign(), s => s
            .SetBlobPath("./DasBook.msix")
            .SetBundle("./attest.bundle")
            .SetCertificateIdentityRegexp(".+@brewingcoder.com$")
            .SetCertificateOidcIssuer("https://token.actions.githubusercontent.com")
            .SetPredicateType("https://slsa.dev/provenance/v1"));
        Assert.Contains("--bundle", plan.Arguments);
        Assert.Contains("--certificate-identity-regexp", plan.Arguments);
        Assert.Contains("--certificate-oidc-issuer", plan.Arguments);
        Assert.Contains("--type", plan.Arguments);
        Assert.Equal("./DasBook.msix", plan.Arguments[^1]);
    }

    [Fact]
    public void Cosign_VerifyBlobAttestation_Identity_And_Regexp_Mutually_Exclusive()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GhAttest.Cosign.VerifyBlobAttestation(FakeCosign(), s => s
                .SetBlobPath("x")
                .SetCertificateIdentity("a@b.com")
                .SetCertificateIdentityRegexp(".+@b.com$")
                .SetCertificateOidcIssuer("https://issuer.example")).Arguments.ToList());
    }

    [Fact]
    public void Cosign_VerifyBlobAttestation_Issuer_And_Regexp_Mutually_Exclusive()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GhAttest.Cosign.VerifyBlobAttestation(FakeCosign(), s => s
                .SetBlobPath("x")
                .SetCertificateIdentity("a@b.com")
                .SetCertificateOidcIssuer("https://x")
                .SetCertificateOidcIssuerRegexp(".+")).Arguments.ToList());
    }

    [Fact]
    public void Cosign_VerifyBlobAttestation_CaRoots_And_Certificate_Mutually_Exclusive()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GhAttest.Cosign.VerifyBlobAttestation(FakeCosign(), s => s
                .SetBlobPath("x")
                .SetKey("cosign.pub")
                .SetCaRoots("./ca.pem")
                .SetCertificate("./cert.pem")).Arguments.ToList());
    }

    [Fact]
    public void Cosign_VerifyBlobAttestation_BlobPath_Required()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GhAttest.Cosign.VerifyBlobAttestation(FakeCosign(), s => s
                .SetKey("k.pub")).Arguments.ToList());
    }

    [Fact]
    public void Cosign_VerifyBlobAttestation_Identity_Selector_Required()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GhAttest.Cosign.VerifyBlobAttestation(FakeCosign(), s => s
                .SetBlobPath("x")).Arguments.ToList());
    }

    // ─── cosign verify-blob / verify-attestation / verify / tree ─────────

    [Fact]
    public void Cosign_VerifyBlob_With_Bundle()
    {
        var plan = GhAttest.Cosign.VerifyBlob(FakeCosign(), s => s
            .SetBlobPath("./blob.bin").SetBundle("./b.bundle"));
        Assert.Equal("verify-blob", plan.Arguments[0]);
        Assert.Contains("--bundle", plan.Arguments);
        Assert.Equal("./blob.bin", plan.Arguments[^1]);
    }

    [Fact]
    public void Cosign_VerifyAttestation_For_Image()
    {
        var plan = GhAttest.Cosign.VerifyAttestation(FakeCosign(), s => s
            .SetImage("ghcr.io/x/y:1.0")
            .SetCertificateIdentity("scott@example.com")
            .SetCertificateOidcIssuer("https://issuer")
            .SetPredicateType("https://slsa.dev/provenance/v1"));
        Assert.Equal("verify-attestation", plan.Arguments[0]);
        Assert.Equal("https://slsa.dev/provenance/v1",
            plan.Arguments[IndexOf(plan.Arguments, "--type") + 1]);
        Assert.Equal("ghcr.io/x/y:1.0", plan.Arguments[^1]);
    }

    [Fact]
    public void Cosign_Verify_Image_With_Key()
    {
        var plan = GhAttest.Cosign.Verify(FakeCosign(), s => s
            .SetImage("ghcr.io/x/y:1.0").SetKey("cosign.pub"));
        Assert.Equal("verify", plan.Arguments[0]);
        Assert.Equal("ghcr.io/x/y:1.0", plan.Arguments[^1]);
    }

    [Fact]
    public void Cosign_Tree_For_Image()
    {
        var plan = GhAttest.Cosign.Tree(FakeCosign(), s => s.SetImage("ghcr.io/x/y:1.0"));
        Assert.Equal(new[] { "tree", "ghcr.io/x/y:1.0" }, plan.Arguments);
    }

    [Fact]
    public void Cosign_Tree_Image_Required()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GhAttest.Cosign.Tree(FakeCosign(), s => { }).Arguments.ToList());
    }

    [Fact]
    public void Cosign_Version_Verb()
    {
        var plan = GhAttest.Cosign.Version(FakeCosign());
        Assert.Equal(new[] { "version" }, plan.Arguments);
    }

    [Fact]
    public void Cosign_Initialize_With_Mirror_And_Root()
    {
        var plan = GhAttest.Cosign.Initialize(FakeCosign(), s => s
            .SetMirror("https://tuf-repo.example.com")
            .SetRoot("./trusted-root.json"));
        Assert.Equal("initialize", plan.Arguments[0]);
        Assert.Equal("https://tuf-repo.example.com",
            plan.Arguments[IndexOf(plan.Arguments, "--mirror") + 1]);
        Assert.Equal("./trusted-root.json",
            plan.Arguments[IndexOf(plan.Arguments, "--root") + 1]);
    }

    // ─── raw escape hatches ───────────────────────────────────────────────

    [Fact]
    public void GhAttest_Raw()
    {
        var plan = GhAttest.Raw(FakeGh(), "attestation", "experimental", "foo");
        Assert.Equal(new[] { "attestation", "experimental", "foo" }, plan.Arguments);
    }

    [Fact]
    public void Cosign_Raw()
    {
        var plan = GhAttest.Cosign.Raw(FakeCosign(), "piv-tool", "list-slots");
        Assert.Equal(new[] { "piv-tool", "list-slots" }, plan.Arguments);
    }

    [Fact]
    public void GhAttest_Raw_Rejects_Empty()
    {
        Assert.Throws<ArgumentException>(() => GhAttest.Raw(FakeGh()));
    }

    [Fact]
    public void Cosign_Raw_Rejects_Empty()
    {
        Assert.Throws<ArgumentException>(() => GhAttest.Cosign.Raw(FakeCosign()));
    }

    // ─── shared knobs ─────────────────────────────────────────────────────

    [Fact]
    public void Verify_WorkingDirectory_Propagates()
    {
        var plan = GhAttest.Verify(FakeGh(), s => s
            .SetSubjectFile("x").SetOwner("o").SetWorkingDirectory("/repo"));
        Assert.Equal("/repo", plan.WorkingDirectory);
    }

    [Fact]
    public void Cosign_Verbose_Flag()
    {
        var plan = GhAttest.Cosign.Verify(FakeCosign(), s => s
            .SetImage("x:1").SetKey("k").SetVerbose());
        Assert.Contains("-d", plan.Arguments);
    }
}
