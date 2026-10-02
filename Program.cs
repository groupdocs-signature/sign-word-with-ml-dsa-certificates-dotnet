// Topic: Sign Word documents with post-quantum ML-DSA certificates and verify them.
// Uses GroupDocs.Signature for .NET 26.9+: DigitalSignOptions with an ML-DSA PFX file,
// DigitalVerifyOptions with the public certificate, and Search for digital signatures.

using GroupDocs.Signature;
using GroupDocs.Signature.Domain;
using GroupDocs.Signature.Options;

namespace Demo.PostQuantumWordSigning;

internal static class Program
{
    private const string DocsFolder = "documents";
    private const string ResultFolder = "Result";

    private static readonly string SourceDocx = Path.Combine(DocsFolder, "contract.docx");

    // Self-signed ML-DSA test certificates, valid from 2026 to 2056.
    // Use certificates from your own CA in production.
    private static readonly string MlDsa44Pfx = Path.Combine(DocsFolder, "mldsa44.pfx");
    private static readonly string MlDsa65Pfx = Path.Combine(DocsFolder, "mldsa65.pfx");
    private static readonly string MlDsa87Pfx = Path.Combine(DocsFolder, "mldsa87.pfx");
    private static readonly string MlDsa65Cer = Path.Combine(DocsFolder, "mldsa65.cer");
    private const string CertificatePassword = "1234567890";

    private static int Main()
    {
        Directory.CreateDirectory(DocsFolder);
        Directory.CreateDirectory(ResultFolder);
        ApplyLicense();

        if (!File.Exists(SourceDocx))
        {
            Console.Error.WriteLine(
                $"Missing source document: {Path.GetFullPath(SourceDocx)}");
            return 1;
        }

        string signedDocx = Path.Combine(ResultFolder, "contract-signed.docx");
        string signer = SignWithMlDsaCertificate(SourceDocx, MlDsa65Pfx, signedDocx);
        Console.WriteLine($"Signed with ML-DSA-65 by: {signer}");

        Console.WriteLine("Signed files for each ML-DSA security level:");
        foreach (KeyValuePair<string, long> file in SignWithEachSecurityLevel(SourceDocx))
        {
            Console.WriteLine($"  {file.Key}: {file.Value} bytes");
        }

        bool bySigner = VerifySigner(signedDocx, MlDsa65Cer, null);
        bool byOther = VerifySigner(signedDocx, MlDsa44Pfx, CertificatePassword);
        Console.WriteLine($"Valid for the signer's public certificate : {bySigner}");
        Console.WriteLine($"Valid for another signer's certificate    : {byOther}");

        int found = ListDigitalSignatures(signedDocx);
        Console.WriteLine($"Results: {Path.GetFullPath(ResultFolder)}");

        return bySigner && !byOther && found == 1 ? 0 : 2;
    }

    private static void ApplyLicense()
    {
        // Point this at your .lic file to remove evaluation limits.
        // Get a free temporary licence: https://purchase.groupdocs.com/temporary-license
        const string licensePath = "REPLACE_WITH_YOUR_LICENSE_PATH";
        if (File.Exists(licensePath))
        {
            new License().SetLicense(licensePath);
            Console.WriteLine("[license] applied");
        }
        else
        {
            Console.WriteLine("[license] no licence set - running in evaluation mode");
        }
    }

    /// <summary>
    /// Signs a Word document with a post-quantum ML-DSA certificate.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="DigitalSignOptions"/> with the path of a PFX file that holds an
    /// ML-DSA key and its password, exactly as for an RSA certificate, and calls
    /// <c>Sign</c>. ML-DSA (FIPS 204) is the NIST post-quantum signature algorithm,
    /// meant to stay secure when quantum computers can break RSA and ECDSA. Since
    /// GroupDocs.Signature 26.9 this works for Word documents (DOCX, DOC, ODT and the
    /// other Word formats) on every supported platform: where .NET itself cannot read
    /// ML-DSA keys, for example on Linux with .NET 8, the certificate read by the Word
    /// engine is used. PDF, spreadsheets and presentations cannot be signed with ML-DSA
    /// yet. There is no standard XML-DSig identifier for ML-DSA yet, so Microsoft Word
    /// may not validate such a signature. Writes the signed document to
    /// <paramref name="outputPath"/> and returns the subject of the signing
    /// certificate.
    /// </remarks>
    public static string SignWithMlDsaCertificate(
        string sourcePath, string pfxPath, string outputPath)
    {
        using var signature = new Signature(sourcePath);

        var options = new DigitalSignOptions(pfxPath)
        {
            Password = CertificatePassword
        };

        SignResult result = signature.Sign(outputPath, options);
        var created = result.Succeeded.OfType<DigitalSignature>().FirstOrDefault();
        return created?.Certificate?.Subject ?? "(no certificate returned)";
    }

    /// <summary>
    /// Signs the same Word document with ML-DSA-44, ML-DSA-65 and ML-DSA-87
    /// certificates.
    /// </summary>
    /// <remarks>
    /// Calls <c>Sign</c> with a <see cref="DigitalSignOptions"/> for each of the three
    /// ML-DSA parameter sets. They trade size for strength: ML-DSA-44 targets NIST
    /// security category 2, ML-DSA-65 category 3 and ML-DSA-87 category 5, and the
    /// signature and the key grow with the level, which the file sizes show. ML-DSA-65
    /// is a balanced choice when no policy prescribes a level; some government
    /// profiles, such as CNSA 2.0, require ML-DSA-87. Writes one signed file per level
    /// to the result folder and returns the file name and size of each.
    /// </remarks>
    public static Dictionary<string, long> SignWithEachSecurityLevel(string sourcePath)
    {
        var levels = new Dictionary<string, string>
        {
            ["ML-DSA-44"] = MlDsa44Pfx,
            ["ML-DSA-65"] = MlDsa65Pfx,
            ["ML-DSA-87"] = MlDsa87Pfx
        };

        var sizes = new Dictionary<string, long>();
        foreach (KeyValuePair<string, string> level in levels)
        {
            string suffix = level.Key.Replace("-", "").ToLowerInvariant();
            string fileName = $"contract-{suffix}.docx";
            string outputPath = Path.Combine(ResultFolder, fileName);

            using var signature = new Signature(sourcePath);
            var options = new DigitalSignOptions(level.Value)
            {
                Password = CertificatePassword
            };
            signature.Sign(outputPath, options);

            sizes[$"{level.Key} -> {fileName}"] = new FileInfo(outputPath).Length;
        }

        return sizes;
    }

    /// <summary>
    /// Checks that a signed Word document was signed by the owner of a given
    /// certificate.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="DigitalVerifyOptions"/> with a certificate file and calls
    /// <c>Verify</c>. Recipients only need the signer's public certificate (a .cer
    /// file); a PFX with its password works too. The result is valid only when the
    /// signature matches the document content and its certificate has the same serial
    /// number and thumbprint as the one given, so a document signed by someone else, or
    /// changed after signing, is not valid. Returns <c>true</c> when the verification
    /// succeeds.
    /// </remarks>
    public static bool VerifySigner(
        string signedPath, string certificatePath, string? password)
    {
        using var signature = new Signature(signedPath);

        var options = new DigitalVerifyOptions(certificatePath);
        if (password != null)
        {
            options.Password = password;
        }

        VerificationResult result = signature.Verify(options);
        return result.IsValid;
    }

    /// <summary>
    /// Lists the digital signatures of a document with their certificates and validity.
    /// </summary>
    /// <remarks>
    /// Calls <c>Search&lt;DigitalSignature&gt;</c> with
    /// <see cref="SignatureType.Digital"/>. Each <see cref="DigitalSignature"/> carries
    /// the signing certificate, the signing time and <c>IsValid</c>, which tells
    /// whether the signature still matches the document. Use it to show who signed an
    /// incoming document before you process it. For an ML-DSA signature the returned
    /// certificate is the public certificate. Prints one line per signature and returns
    /// the number found.
    /// </remarks>
    public static int ListDigitalSignatures(string signedPath)
    {
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
    }
}
