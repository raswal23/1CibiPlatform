// Sample inputs
using ATS.Services.FilePDFService;

var applicantName = "John Doe";
var signedDate = DateOnly.FromDateTime(DateTime.UtcNow);
byte[] sampleFileContent = File.ReadAllBytes("C:\\Users\\rcgu\\Documents\\GitHub\\1CibiPlatform\\Tools\\PdfPreview\\signature.png");
; // or load bytes from a local PNG/JPG file

// Create service and generate PDF
var service = new FilePdfService();
using var pdfStream = await service.GenerateConsentFormPdfAsync(applicantName, signedDate, sampleFileContent);

// Save to disk
var outPath = Path.Combine(Environment.CurrentDirectory, "consent_preview.pdf");
using var outFile = File.Create(outPath);
await pdfStream.CopyToAsync(outFile);

Console.WriteLine($"Saved preview to: {outPath}");