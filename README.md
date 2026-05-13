# Tamp.GitHubAttest

> Wrapper for `gh attestation` (verify / download / trusted-root) plus the cosign verify-side surface. Covers the SLSA build-provenance side of supply-chain hygiene. Pairs with [`Tamp.Syft`](https://github.com/tamp-build/tamp-syft) (SBOM = what's inside) for the full "what is it + how was it built" story.

| Package | Status |
|---|---|
| `Tamp.GitHubAttest` | 0.1.0 (initial) |

## Why this exists

GitHub's [artifact attestations](https://docs.github.com/en/actions/concepts/security/artifact-attestations) feature lets a CI workflow emit a signed SLSA-v1 build-provenance attestation for any artifact it produces — binaries, container images, npm packages, MSIX, anything. Verifying that attestation downstream is what proves "this artifact came from this workflow on this commit." It's the **how was it built** complement to a SBOM's **what's inside**.

`Tamp.GitHubAttest` makes verification a typed step in the Tamp build graph for two paths:

- **`gh attestation`** (GitHub CLI 2.x) — the canonical online verify path. Pulls attestation bundles from GitHub's attestation API and checks them against the signer identity claims you specify.
- **cosign** (Sigstore) — the generic verify-side surface for off-GHA scenarios: verifying a downloaded bundle in air-gapped environments, container-image attestations, keyless + keyed Sigstore flows.

## Scope — verification only

**Attestation GENERATION happens in CI via the [`actions/attest-build-provenance`](https://github.com/actions/attest-build-provenance) GitHub Action, not via this satellite.** That's a workflow-level concern — emit-side runs as part of the build that produces the artifact, signed via Sigstore's ambient identity and the GHA workflow's OIDC token. There's no CLI verb to wrap for that side.

What `Tamp.GitHubAttest` covers:

| Verb | Tool | Concern |
|---|---|---|
| `GhAttest.Verify` | gh CLI | Verify an artifact's signed attestation chain |
| `GhAttest.Download` | gh CLI | Pull attestations to disk for offline verify |
| `GhAttest.TrustedRoot` | gh CLI | Emit trusted-root config for air-gapped verify |
| `GhAttest.Cosign.VerifyBlobAttestation` | cosign | Verify a local blob against a downloaded attestation |
| `GhAttest.Cosign.VerifyBlob` / `Verify` | cosign | Verify signatures (no attestation predicate) |
| `GhAttest.Cosign.VerifyAttestation` | cosign | Verify a container image's attestation |
| `GhAttest.Cosign.Tree` / `Version` / `Initialize` | cosign | Diagnostics + TUF root bootstrap |

## Install

```bash
dotnet add package Tamp.GitHubAttest
```

Multi-targets net8 / net9 / net10. Requires `Tamp.Core` ≥ **1.6.0**.

## Tool installation

Both tools are pre-installed on GitHub Actions runners. For local dev / Azure DevOps self-hosted:

- **gh:** `brew install gh` / `winget install GitHub.cli`
- **cosign:** `brew install cosign` / `winget install sigstore.cosign`

## Quick start — DasBook MSIX verification

Imagine DasBook publishes its MSIX via the workflow `release.yml`, which uses `actions/attest-build-provenance` to attest the built file. Downstream verification:

```csharp
using Tamp;
using Tamp.GitHubAttest;

class Build : TampBuild
{
    public static int Main(string[] args) => Execute<Build>(args);

    [FromPath("gh")]     readonly Tool GhCli = null!;
    [FromPath("cosign")] readonly Tool CosignTool = null!;

    AbsolutePath Msix => RootDirectory / "artifacts" / "DasBook_1.0.6.0_x64.msix";

    // Online verify — talks to GitHub's attestation API
    Target VerifyProvenance => _ => _
        .Description("[Compliance] Verify the MSIX was built by the official release workflow")
        .Executes(() => GhAttest.Verify(GhCli, s => s
            .SetSubjectFile(Msix)
            .SetRepo("BrewingCoder/dasbook")
            .SetSignerWorkflow("BrewingCoder/dasbook/.github/workflows/release.yml")
            .SetSourceRef("refs/tags/v1.0.6")
            .SetFormatJson()));

    // Offline verify — download once, verify many times with cosign
    Target VerifyOffline => _ => _
        .Description("[Compliance] Verify against a previously-downloaded bundle (air-gapped)")
        .Executes(() =>
        {
            // Step 1: download the attestation bundle (once, online)
            // GhAttest.Download(GhCli, s => s.SetSubjectFile(Msix).SetRepo("BrewingCoder/dasbook"));
            //   → writes attestations.jsonl alongside the artifact

            // Step 2: verify offline using cosign + downloaded bundle
            return GhAttest.Cosign.VerifyBlobAttestation(CosignTool, s => s
                .SetBlobPath(Msix)
                .SetBundle(Msix.WithName(Msix.Name + ".sigstore"))
                .SetCertificateIdentityRegexp("^https://github.com/BrewingCoder/dasbook/")
                .SetCertificateOidcIssuer("https://token.actions.githubusercontent.com")
                .SetPredicateType("https://slsa.dev/provenance/v1"));
        });
}
```

## The signer-identity claim — what makes verification meaningful

`gh attestation verify` requires either `--owner` or `--repo` to scope which org/repo's workflows are trusted as signers. **Without a signer-identity claim, the verification is meaningless** — any GHA workflow on GitHub can sign attestations and pass a content-integrity check. The identity is what proves "this artifact came from THIS repo's THIS workflow."

Add `SetSignerWorkflow(...)` for stricter "this exact reusable workflow path" enforcement — the recommended pattern for production verifications:

```csharp
.SetSignerWorkflow("BrewingCoder/dasbook/.github/workflows/release.yml")
.SetSourceRef("refs/tags/v1.0.6")                  // Optional: enforce tag was the source
.SetSignerDigest("abc123...")                       // Optional: enforce workflow file digest
```

For cosign's keyless verification:

```csharp
.SetCertificateIdentityRegexp("^https://github.com/BrewingCoder/dasbook/")
.SetCertificateOidcIssuer("https://token.actions.githubusercontent.com")
```

The wrapper validates that keyless flows have an `--certificate-oidc-issuer` set — TAMP-style fail-fast for the missing-identity-claim class of bug.

## Mutual-exclusion enforcement

The wrapper catches common config mistakes at `ToCommandPlan(...)` time:

- `--owner` and `--repo` are mutually exclusive (gh attestation).
- `CertificateIdentity` and `CertificateIdentityRegexp` are mutually exclusive (cosign).
- `CertificateOidcIssuer` and `CertificateOidcIssuerRegexp` are mutually exclusive (cosign).
- `CaRoots` and `Certificate` are mutually exclusive trust-model flag families (cosign).
- `--format` validated against {`json`} (the only supported value).
- Digest algorithm validated against {`sha256`, `sha512`}.
- `--limit` validated ≥ 1.
- Keyless verification requires an OIDC issuer setter.

All as `InvalidOperationException` thrown *before* the slow tool launches.

## Pairs with

- **[`Tamp.Syft`](https://github.com/tamp-build/tamp-syft)** — SBOM. The "what's inside" half of supply-chain hygiene. Verify *what's in the package* with Syft; verify *who built it and from where* with this satellite.
- **[`Tamp.Grype`](https://github.com/tamp-build/tamp-grype)** — CVE scanner. Reads Syft SBOMs.
- **[`Tamp.TruffleHog.V3`](https://www.nuget.org/packages/Tamp.TruffleHog.V3)** + **[`Tamp.CodeQL.V2`](https://www.nuget.org/packages/Tamp.CodeQL.V2)** — orthogonal axes (secrets, code patterns).
- **[`Tamp.GitHubCli.V2`](https://www.nuget.org/packages/Tamp.GitHubCli.V2)** — the parent gh CLI wrapper. `Tamp.GitHubAttest` is the focused attestation-only slice; for general gh CLI usage (release, pr, issue, etc.) use that one.

## Releasing

Releases follow the [Tamp dogfood pattern](MAINTAINERS.md).

## License

MIT. See [LICENSE](LICENSE).
