# Post-Quantum ML-DSA Signing for Word Documents

[![Product Page](https://img.shields.io/badge/Product%20Page-2865E0?style=for-the-badge&logo=appveyor&logoColor=white)](https://github.com/groupdocs-signature/GroupDocs.Signature-Docs)
[![Docs](https://img.shields.io/badge/Docs-2865E0?style=for-the-badge&logo=Hugo&logoColor=white)](https://docs.groupdocs.com/signature/net/)
[![Blog](https://img.shields.io/badge/Blog-2865E0?style=for-the-badge&logo=WordPress&logoColor=white)](https://blog.groupdocs.com/categories/groupdocs.signature-product-family/)
[![Free Support](https://img.shields.io/badge/Free%20Support-2865E0?style=for-the-badge&logo=Discourse&logoColor=white)](https://forum.groupdocs.com/c/signature/13)
[![Temporary License](https://img.shields.io/badge/Temporary%20License-2865E0?style=for-the-badge&logo=rocket&logoColor=white)](https://purchase.groupdocs.com/temp-license/100124)

## Introduction

`sign-word-with-ml-dsa-certificates-dotnet` is a runnable .NET 8 console sample that signs a Word document with post-quantum ML-DSA certificates, at all three security levels, and verifies the result against the signer's public certificate. ML-DSA is the NIST post-quantum signature algorithm standardised as FIPS 204, intended to stay sound once quantum computers can break RSA and ECDSA.

Support arrived in GroupDocs.Signature 26.9 for Word formats - DOCX, DOC, ODT and the rest of the Word family - on every supported platform. The calling code is the same as for an RSA certificate: a PFX path, a password, `DigitalSignOptions`.

## Use Case Scenarios

Long-retention archives are the obvious case. A contract signed today and kept for thirty years has to survive whatever arrives in that window, and "harvest now, decrypt later" is a real threat model for anything with that lifespan. Procurement is the second: some government profiles, CNSA 2.0 among them, already name ML-DSA-87 specifically. The third is less dramatic and more common - a compliance team that wants evidence the organisation can produce a post-quantum signature at all, before a deadline makes it urgent.

## The Problem

Post-quantum signing is usually described as a migration, which makes it sound like a project. In practice the first question is narrower: can the document library you already use emit an ML-DSA signature, and can anyone verify it afterwards?

Two things complicate the answer. The first is platform support for the keys themselves: .NET cannot read ML-DSA keys everywhere yet, notably on Linux with .NET 8. The second is validation - there is no standard XML-DSig identifier for ML-DSA, so Microsoft Word may not show the signature as valid even when it is cryptographically sound.

### Common Challenges

- ❌ Key support differs between platforms and .NET versions, so a sample that works on Windows may not run on a Linux build agent
- ❌ Format support is partial: Word formats only, with PDF, spreadsheets and presentations still to come
- ❌ Third-party validators, Word included, may not recognise the algorithm yet

## The Solution

GroupDocs.Signature handles the key-reading problem internally: where .NET itself cannot read an ML-DSA key, the certificate is read through the Word engine instead, so the same code works on Linux with .NET 8 as on Windows. That is the whole of the platform workaround, and it needs nothing from the caller.

✅ **Same API as RSA** - `DigitalSignOptions(pfxPath)` with a password; no separate code path
✅ **All three parameter sets** - ML-DSA-44, ML-DSA-65 and ML-DSA-87, selected simply by which PFX you pass
✅ **Verification by public certificate** - recipients need only a `.cer`, not a PFX
✅ **Signature discovery** - `Search<DigitalSignature>` reports the certificate, signing time and validity

## Implementation Workflow

1. **Obtain ML-DSA certificates** - from your CA, or self-signed for testing
2. **Sign** - `DigitalSignOptions` with the PFX path and password, then `Sign`
3. **Choose a level** - 44, 65 or 87 depending on policy; the file size shows the cost
4. **Verify** - `DigitalVerifyOptions` with the signer's public certificate
5. **Inspect** - `Search<DigitalSignature>` to list what a received document carries

### Which ML-DSA level should I pick?

ML-DSA-65 unless something tells you otherwise. The three parameter sets map to NIST security categories 2, 3 and 5, and the signature and key grow with the level - which the sample makes visible by printing the size of each signed file. Pick 87 when a profile such as CNSA 2.0 requires it, and 44 only when size matters more than margin.

## Requirements

- **.NET SDK 8.0** - the project targets `net8.0`
- **GroupDocs.Signature 26.9.0** - ML-DSA support for Word formats starts here
- **ML-DSA certificates** - the sample ships self-signed test ones valid 2026 to 2056
- **Licence (optional)** - evaluation mode still signs but adds its own marks

## Project Structure

```
sign-word-with-ml-dsa-certificates-dotnet/
│
├── Program.cs
├── PostQuantumWordSigningDemo.csproj
├── documents/
│   ├── contract.docx
│   ├── mldsa44.pfx
│   ├── mldsa65.pfx
│   ├── mldsa65.cer
│   └── mldsa87.pfx
└── Result/
    ├── contract-signed.docx
    ├── contract-mldsa44.docx
    ├── contract-mldsa65.docx
    └── contract-mldsa87.docx
```

**File Organization:**
- **Program.cs** - the four methods below, run in sequence with a pass/fail exit code
- **documents/contract.docx** - the document being signed
- **documents/*.pfx** - self-signed ML-DSA test certificates, one per security level
- **documents/mldsa65.cer** - the public half of the ML-DSA-65 certificate, used for verification
- **Result/** - one signed copy per level, plus the main signed output

## Practical Examples

### Use Case: Signs a Word document with a post-quantum ML-DSA certificate

Use it when the document has to outlive RSA. The call is deliberately unremarkable - that is the point of the feature.

```csharp
using var signature = new Signature(sourcePath);

var options = new DigitalSignOptions(pfxPath)
{
    Password = CertificatePassword
};

SignResult result = signature.Sign(outputPath, options);
var created = result.Succeeded.OfType<DigitalSignature>().FirstOrDefault();
return created?.Certificate?.Subject ?? "(no certificate returned)";
```

What This Solves: an ML-DSA signature on a Word document with no special-casing in your code. I expected to need a separate code path for this and wrote one before discovering the certificate alone decides the algorithm.

In practice: the returned `Subject` is the quickest confirmation that the certificate the library used is the one you intended, which matters when several are in play.

### Use Case: Signs the same Word document with ML-DSA-44, ML-DSA-65 and ML-DSA-87 certificates

Running all three side by side is the clearest way to see what the security level costs.

```csharp
var levels = new Dictionary<string, string>
{
    ["ML-DSA-44"] = MlDsa44Pfx,
    ["ML-DSA-65"] = MlDsa65Pfx,
    ["ML-DSA-87"] = MlDsa87Pfx
};
```

Each level signs the same source into its own output, and the file size is recorded:

```csharp
using var signature = new Signature(sourcePath);
var options = new DigitalSignOptions(level.Value)
{
    Password = CertificatePassword
};
signature.Sign(outputPath, options);

sizes[$"{level.Key} -> {fileName}"] = new FileInfo(outputPath).Length;
```

What This Solves: a concrete answer to "how much bigger is 87 than 44" for your own documents, rather than a table from a specification.

In practice: for a short contract the difference is modest; for a system storing millions of signed documents it is worth measuring before standardising on the highest level.

### Use Case: Checks that a signed Word document was signed by the owner of a given certificate

Verification needs only the public certificate, which is what makes distribution practical.

```csharp
using var signature = new Signature(signedPath);

var options = new DigitalVerifyOptions(certificatePath);
if (password != null)
{
    options.Password = password;
}

VerificationResult result = signature.Verify(options);
return result.IsValid;
```

What This Solves: proof that a specific party signed, not merely that some signature exists.

In practice: the sample runs this twice - once with the signer's `.cer`, which returns true, and once with a different signer's PFX, which returns false. Both assertions matter; a verification routine that only ever sees valid input is not tested.

### Use Case: Lists the digital signatures of a document with their certificates and validity

For incoming documents, the first question is what is already on them.

```csharp
using var signature = new Signature(signedPath);

List<DigitalSignature> found =
    signature.Search<DigitalSignature>(SignatureType.Digital);
foreach (DigitalSignature item in found)
{
    Console.WriteLine(
        $"Digital signature: {item.Certificate?.Subject}, " +
        $"signed {item.SignTime:u}, valid: {item.IsValid}");
}

return found.Count;
```

What This Solves: an inventory of signer, time and validity before your pipeline decides what to do with a file.

In practice: for an ML-DSA signature the certificate that comes back is the public certificate, which is all a recipient needs.

## Benefits

The migration cost is close to zero at the call site: a different PFX, the same options object. All three NIST parameter sets are available immediately, so a policy change is a configuration change rather than a code change. Verification works with a public certificate, so you can distribute trust without distributing keys. And because the platform gap is handled inside the library, the same build runs on a Windows developer machine and a Linux container.

## Related Use Cases and Resources

If you're planning post-quantum work with document signatures, these resources will help you:

* **Step-by-step use case guide in the documentation** - the integration path, platform caveats and verification: [Read the article →](https://docs.groupdocs.com/signature/net/use-cases/sign-word-with-post-quantum-certificates/)

* **In-depth blog article about this project** - what "post-quantum ready" means for a document pipeline today: [Read the article →](https://blog.groupdocs.com/signature/sign-word-with-post-quantum-certificates-net/)

* **Sign Document with Digital Signature** - the `DigitalSignOptions` reference this builds on: [Read the article →](https://docs.groupdocs.com/signature/net/sign-document-with-digital-signature/)

* **Verify Digital Signatures in the Document** - verification options, including certificate-based checks: [Read the article →](https://docs.groupdocs.com/signature/net/verify-digital-signatures-in-the-document/)

## Keywords

`post-quantum`, `ml-dsa`, `fips 204`, `dilithium`, `word signing`, `docx signature`, `digital signature`, `groupdocs signature`, `dotnet signing`, `cnsa 2.0`, `quantum-safe`, `digitalsignoptions`, `digitalverifyoptions`, `certificate verification`, `ml-dsa-44`, `ml-dsa-65`, `ml-dsa-87`, `net8`, `long-term archive`, `pfx certificate`, `signature search`, `26.9`

**Ready to get started?** [View Documentation](https://docs.groupdocs.com/signature/net/) | [Get Support](https://forum.groupdocs.com/c/signature/13) | [Request License](https://purchase.groupdocs.com/temp-license/100124)
