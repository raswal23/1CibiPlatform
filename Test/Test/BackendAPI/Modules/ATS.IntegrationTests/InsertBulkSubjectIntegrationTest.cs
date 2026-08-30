using ATS.Data.DTO;
using ATS.Features.Web.InsertBulkSubject;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class InsertBulkSubjectIntegrationTest : BaseIntegrationTest
{
	private readonly string _atsTestFolder;
	byte[] sampleFileContent = Convert.FromBase64String("SGVsbG8gV29ybGQ=");
	string bulkFileName = $"{Guid.CreateVersion7()}-bulkfile.csv";

	public InsertBulkSubjectIntegrationTest(IntegrationTestWebAppFactory factory) : base(factory)
	{
		_atsTestFolder = _configuration
				.GetSection("AlibabaOss")
				.GetValue<string>("ATSTestFolder") ?? string.Empty;
	}

	private IFormFile CreateFakeFormFile(byte[] content, string fileName)
	{
		var stream = new MemoryStream(content);
		return new FormFile(stream, 0, content.Length, "file", fileName)
		{
			Headers = new HeaderDictionary(),
			ContentType = "text/csv"
		};
	}

	#region Positive Path
	[Fact]

	public async Task InsertBulkSubject_ShouldReturnCreatedIdAndPersist()
	{
		// Arrange
		// The package applies to every row, so it must be assigned to the caller's
		// client or the upload is rejected before the file is stored.
		var package = await SeedAssignedPackageAsync("Air BnB");

		var dto = new BulkUploadFileDetailsDTO
		{
			BulkFile = CreateFakeFormFile(sampleFileContent, bulkFileName),
			FileName = bulkFileName,
			Status = "Pending",
			OrderType = "Rush",
			PackageId = DefaultPackageId,
			PackageType = package
		};

		var command = new InsertBulkSubjectCommand(dto);
		// Act
		var result = await _sender.Send(command);

		// Assert
		result.isAdded.Should().BeTrue();

		if (result.isAdded == true)
		{
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{bulkFileName}");
		}
	}

	#endregion

	#region Negative Path
	[Fact]
	public async Task InsertBulkSubject_ShouldThrowValidationException_WhenFileNameIsEmpty()
	{
		// Arrange
		var dto = new BulkUploadFileDetailsDTO
		{
			FileName = string.Empty,
			Status = "Pending"
		};

		var command = new InsertBulkSubjectCommand(dto);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		var exception = await act.Should()
			.ThrowAsync<FluentValidation.ValidationException>();

		exception.Which.Errors.Should()
			.Contain(x => x.PropertyName.Contains("FileName"));
	}

	[Fact]
	public async Task InsertBulkSubject_ShouldThrowValidationException_WhenBulkFileIsNotCsv()
	{
		// Arrange
		var invalidFileName = $"{Guid.CreateVersion7()}-bulkfile.xlsx";

		var dto = new BulkUploadFileDetailsDTO
		{
			BulkFile = CreateFakeFormFile(sampleFileContent, invalidFileName),
			FileName = invalidFileName,
			Status = "Pending",
			OrderType = "Rush",
			PackageId = DefaultPackageId,
			PackageType = "Air BnB"
		};

		var command = new InsertBulkSubjectCommand(dto);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		var exception = await act.Should()
			.ThrowAsync<ValidationException>();

		exception.Which.Errors.Should().Contain(e =>
			e.PropertyName.Contains("BulkFile") &&
			e.ErrorMessage == "Only .csv files are allowed.");
	}

	[Fact]
	public async Task InsertBulkSubject_ShouldReturnMultipleValidationErrors_WhenAllFieldsAreInvalid()
	{
		// Arrange
		var dto = new BulkUploadFileDetailsDTO
		{
			FileName = string.Empty,
			Status = string.Empty
		};

		var command = new InsertBulkSubjectCommand(dto);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		var exception = await act.Should()
			.ThrowAsync<ValidationException>();

		exception.Which.Errors.Should().HaveCount(4);
	}
	#endregion
}
