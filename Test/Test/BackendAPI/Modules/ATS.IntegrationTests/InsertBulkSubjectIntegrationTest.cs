using ATS.Data.DTO;
using ATS.Features.InsertBulkSubject;
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
	string bulkFileName = $"{Guid.CreateVersion7()}-bulkfile.txt";

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
			ContentType = "text/plain"
		};
	}

	[Fact]

	public async Task InsertBulkSubject_ShouldReturnCreatedIdAndPersist()
	{
		// Arrange
		var dto = new BulkUploadFileDetailsDTO
		{
			BulkFile = CreateFakeFormFile(sampleFileContent, bulkFileName),
			FileName = bulkFileName,
			Status = "Pending",
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

	[Fact]
	public async Task InsertBulkSubject_ShouldThrowValidationException_WhenFileIdIsEmpty()
	{
		// Arrange
		var dto = new BulkUploadFileDetailsDTO
		{
			FileName = "testfile.csv",
			Status = "Pending"
		};

		var command = new InsertBulkSubjectCommand(dto);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		var exception = await act.Should()
			.ThrowAsync<ValidationException>();

		exception.Which.Errors.Should()
			.Contain(x => x.PropertyName.Contains("FileID"));
	}

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
	public async Task InsertBulkSubject_ShouldThrowValidationException_WhenStatusIsEmpty()
	{
		// Arrange
		var dto = new BulkUploadFileDetailsDTO
		{
			FileName = "testfile.csv",
			Status = string.Empty
		};

		var command = new InsertBulkSubjectCommand(dto);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		var exception = await act.Should()
			.ThrowAsync<FluentValidation.ValidationException>();

		exception.Which.Errors.Should()
			.Contain(x => x.PropertyName.Contains("Status"));
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

		exception.Which.Errors.Should().HaveCount(3);
	}
}
