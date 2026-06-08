using FluentAssertions;
using FluentValidation;
using ATS.Data.DTO;
using ATS.Features.InsertBulkSubject;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class InsertBulkSubjectIntegrationTest : BaseIntegrationTest
{
	public InsertBulkSubjectIntegrationTest(IntegrationTestWebAppFactory factory) : base(factory)
	{

	}
	#region Positive Path

	[Fact]

	public async Task InsertBulkSubject_ShouldReturnCreatedIdAndPersist()
	{
		// Arrange
		var dto = new BulkUploadFileDetailsDTO
		{
			FileID = Guid.NewGuid(),
			FileName = "testfile.csv",
			Status = "Pending",
		};

		var command = new InsertBulkSubjectCommand(dto);
		// Act
		var result = await _sender.Send(command);
		// Assert
		result.Should().NotBe(Guid.Empty);

		var persisted = await _dbContext.BulkUploadFileDetails.FindAsync(result);

		persisted.Should().NotBeNull();
		persisted!.FileName.Should().Be(dto.FileName);
	}
	#endregion

	#region Negative Path

	[Fact]
	public async Task InsertBulkSubject_ShouldThrowValidationException_WhenFileIdIsEmpty()
	{
		// Arrange
		var dto = new BulkUploadFileDetailsDTO
		{
			FileID = Guid.Empty,
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
			FileID = Guid.NewGuid(),
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
			FileID = Guid.NewGuid(),
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
			FileID = Guid.Empty,
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
	#endregion
}
