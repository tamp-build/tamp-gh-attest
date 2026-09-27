# Changelog

All notable changes to **Tamp.GitHubAttest** are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Package now ships XML documentation files (`.xml`) alongside the assembly, so consumers get IntelliSense and API docs. (Mirrors [tamp-build/tamp#3](https://github.com/tamp-build/tamp/pull/50).)


## [0.1.0] - 2026-05-13

### Added

- Initial release. Wraps GitHub's attestation verify surface (`gh attestation
  verify / download / trusted-root`) plus the cosign verify-side surface
  (`cosign verify-blob-attestation`, `verify-blob`, `verify-attestation`,
  `verify`, `tree`, `version`, `initialize`). Filed under TAM-195.

  **Scope: verification only.** Attestation generation happens in CI via the
  [`actions/attest-build-provenance`](https://github.com/actions/attest-build-provenance)
  GitHub Action — not via the gh CLI. This satellite focuses on the
  consumer-side surface for verifying an artifact's signed SLSA-v1 build
  provenance.

#### `gh attestation` surface

- **`GhAttest.Verify(...)`** — primary verb. `gh attestation verify [subject]
  --owner|--repo ...`. Subject can be a local file (`SetSubjectFile`) or an
  OCI image reference (`SetSubjectOciImage` — adds `oci://` prefix if missing).
  Signer-identity enforcement: `SetSignerRepo`, `SetSignerWorkflow`,
  `SetSignerDigest`, `SetSourceDigest`, `SetSourceRef`. Default
  predicate-type is `https://slsa.dev/provenance/v1`; override via
  `SetPredicateType`. Output formatting via `SetFormatJson`, `SetJqExpression`,
  `SetTemplatePath`. `SetNoPublicGood` for private Sigstore deployments.
- **`GhAttest.Download(...)`** — `gh attestation download` for offline verify
  prep. `SetDigestAlgorithm("sha256"|"sha512")` validated.
- **`GhAttest.TrustedRoot(...)`** — emit trusted-root config for air-gapped
  setups.
- **`GhAttest.Raw(...)`** — escape hatch for gh attestation verbs not yet typed.

#### cosign surface (nested under `GhAttest.Cosign`)

- **`Cosign.VerifyBlobAttestation(...)`** — `cosign verify-blob-attestation`.
  The off-GHA verify path. Three trust models supported:
  - **Keyed**: `SetKey(keyRef)` + `SetSignature(sigRef)`
  - **Bundle**: `SetBundle(path)` — works with the .sigstore bundles
    `actions/attest-build-provenance` produces
  - **Keyless**: `SetCertificateIdentity(...)` (or
    `SetCertificateIdentityRegexp(...)`) + `SetCertificateOidcIssuer(...)`
- **`Cosign.VerifyBlob(...)`** — signature-only verify (no attestation
  predicate).
- **`Cosign.VerifyAttestation(...)`** — container-image attestation verify.
- **`Cosign.Verify(...)`** — container-image signature verify.
- **`Cosign.Tree(...)`** — show supply-chain artifacts (sigs, SBOMs,
  attestations) attached to an image.
- **`Cosign.Version(...)`** — diagnostic.
- **`Cosign.Initialize(...)`** — bootstrap / refresh the Sigstore TUF trust
  root.
- **`Cosign.Raw(...)`** — escape hatch.

### Validation

- `gh attestation`: `--owner` and `--repo` mutually exclusive; subject
  required; one of owner/repo required; format validated against `{json}`;
  digest algorithm against `{sha256, sha512}`; `--limit` ≥ 1.
- `cosign`: identity-selector required (key OR bundle OR cert-identity);
  keyless verifies require an OIDC issuer; identity vs identity-regexp
  mutually exclusive; OIDC-issuer vs OIDC-issuer-regexp mutually exclusive;
  CA-roots vs certificate trust models mutually exclusive.

### Tests

- 45 unit tests covering positive paths + negative cases. Surfaces:
  all source-scheme helpers, OCI prefix idempotency, signer-identity
  enforcement (signer-repo/workflow/digest, source-ref/digest), keyless +
  keyed + bundle cosign paths, all mutual-exclusion validations, hostname
  override (GHES), limit range, format validation, raw escape hatch.

### Requires

- **Tamp.Core ≥ 1.6.0**. Doesn't handle secrets (verification flow is
  identity-based, not credential-based), so no need for TAMP004's approved
  context — the wrapper would compile clean on older Core versions too,
  but 1.6.0 is the new floor for all post-DasBook-wave satellites.

### Notes

- Completes DasBook wishlist #6 (build attestation / SBOM) alongside
  Tamp.Syft (already shipped). Tamp.Syft = "what's inside";
  Tamp.GitHubAttest = "how/where it was built". Both attach to the same
  artifact in the registry; both verify-side surfaces compose into the same
  CI gate.
- Seventh non-.NET-ish satellite under the post-1.6.0 regime. No
  `[InternalsVisibleTo]` entry in Tamp.Core needed.
