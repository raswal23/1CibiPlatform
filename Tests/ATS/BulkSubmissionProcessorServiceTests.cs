using ATS.Services.BulkSubmissionProcessor;
using ATS.Data.Repository;
using ATS.Constants;
using ATS.DTO;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace ATS.Tests;

public class BulkSubmissionProcessorServiceTests
{
    private readonly Mock<IATSRepository> _repositoryMock;
    private readonly Mock<ILogger<BulkSubmissionProcessorService>> _loggerMock;
    private readonly BulkSubmissionProcessorService _service;

    public BulkSubmissionProcessorServiceTests()
    {
        _repositoryMock = new Mock<IATSRepository>();
        _loggerMock = new Mock<ILogger<BulkSubmissionProcessorService>>();
        
        // Note: Other dependencies are mocked as null for this focused test
        _service = new BulkSubmissionProcessorService(
            _repositoryMock.Object,
            null, // serviceScopeFactory
            null, // objectStorageService
            null, // secureToken
            null, // hashService
            null, // hubContext
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessAsync_LogsWarningWhenStatusIsDoneAndRejectedRowsExist()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var fileName = "test-upload.csv";
        var requestor = "John Doe";
        
        var testFile = new BulkUploadFileDetails
        {
            FileID = fileId,
            FileName = fileName,
            Requestor = requestor,
            Status = BulkFileStatus.Done
        };

        var rejectedRows = new List<BulkUploadRejectedRowDTO>
        {
            new BulkUploadRejectedRowDTO { RowNumber = 1, Reason = "Invalid email format" },
            new BulkUploadRejectedRowDTO { RowNumber = 2, Reason = "Missing required field" }
        };
        
        var serializedRejectedRows = JsonSerializer.Serialize(rejectedRows);
        testFile.RejectedRows = serializedRejectedRows;

        _repositoryMock.Setup(x => x.GetBulkUploadFileDetailsAsync())
            .ReturnsAsync(new List<BulkUploadFileDetails> { testFile });
            
        _repositoryMock.Setup(x => x.GetBulkUploadFileDetailByIdAsync(fileId))
            .ReturnsAsync(testFile);

        // Act
        await _service.ProcessAsync(CancellationToken.None);

        // Assert
        _repositoryMock.Verify(x => x.UpdateBulkFileDetailsStatusAsync(
            It.IsAny<List<Guid>>(), 
            BulkFileStatus.Done), Times.Once);
            
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Bulk upload completed with rejected rows")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}