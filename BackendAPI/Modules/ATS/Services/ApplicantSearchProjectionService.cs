namespace ATS.Services;

public class ApplicantSearchProjectionService : IApplicantSearchProjectionService
{
	private readonly ILogger<ApplicantSearchProjectionService> _logger;
	private readonly IATSRepository _atsRepository;
	private readonly IUnitOfWork _unitOfWork;

	public ApplicantSearchProjectionService(
		ILogger<ApplicantSearchProjectionService> logger,
		IATSRepository atsRepository,
		IUnitOfWork unitOfWork)
	{
		_logger = logger;
		_atsRepository = atsRepository;
		_unitOfWork = unitOfWork;
	}

	public async Task ProcessPendingProjectionsAsync(CancellationToken cancellationToken = default)
	{
		var logContext = new
		{
			Action = "ApplicantSearchProjection",
			Step = "ProcessPending",
			Timestamp = DateTime.UtcNow
		};

		var pending = await _atsRepository.GetEmailInvitationRequestsNeedingProjectionAsync(cancellationToken);
		if (pending.Count == 0)
		{
			_logger.LogInformation("No records found for applicant projection update: {@Context}", logContext);
			return;
		}

		await _unitOfWork.BeginTransactionAsync(cancellationToken);
		try
		{
			foreach (var invitation in pending)
			{
				var projection = await _atsRepository.GetApplicantSearchProjectionByIdAsync(invitation.EmailInvitationID, cancellationToken);

				if (projection is null)
				{
					projection = new ApplicantSearchProjection
					{
						EmailInvitationRequestId = invitation.EmailInvitationID
					};
					await _atsRepository.AddApplicantSearchProjectionAsync(projection, cancellationToken);
				}

				MapProjection(projection, invitation);
				invitation.NeedsProjection = false;
				invitation.ProjectionUpdatedAt = DateTime.UtcNow;
			}

			await _unitOfWork.CommitAsync(cancellationToken);
			_logger.LogInformation("Processed {Count} applicant projection records successfully: {@Context}", pending.Count, logContext);
		}
		catch (Exception ex)
		{
			await _unitOfWork.RollbackAsync(cancellationToken);
			_logger.LogError(ex, "Failed applicant projection processing: {@Context}", logContext);
			throw;
		}
	}

	private static void MapProjection(ApplicantSearchProjection projection, EmailInvitationRequest invitation)
	{
		var personal = invitation.PersonalDetails;
		var address = invitation.AddressDetails;
		var educational = invitation.EducationalBackground;
		var licenses = invitation.LicensesDetails;
		var professional = invitation.ProfessionalExperiences;
		var reference = invitation.ReferenceDetails;
		var signature = invitation.SignatureDetails;

		projection.FirstName = invitation.FirstName;
		projection.LastName = invitation.LastName;
		projection.MiddleInitial = invitation.MiddleInitial;
		projection.EmailAddress = invitation.EmailAddress;
		projection.MobileNumber = invitation.MobileNumber;
		projection.SelectPackage = invitation.SelectPackage;
		projection.RushNormal = invitation.RushNormal;
		projection.OrderStatus = invitation.OrderStatus;
		projection.OrderCreatedAt = invitation.OrderCreatedAt;
		projection.OrderCompletedAt = invitation.OrderCompletedAt;
		projection.ApplicationFormStatus = invitation.ApplicationFormStatus;

		projection.PositionAppliedFor = personal?.PositionAppliedFor;
		projection.MaritalStatus = personal?.MaritalStatus;
		projection.Nationality = personal?.Nationality;
		projection.Sex = personal?.Sex;
		projection.DOB = personal?.DOB;
		projection.SSS = personal?.SSS;
		projection.TIN = personal?.TIN;
		projection.EmailAlternative = personal?.EmailAlternative;

		projection.CurrentAddress = address?.CurrentAddress;
		projection.CurrentCity = address?.CurrentCity;
		projection.CurrentProvince = address?.CurrentProvince;
		projection.CurrentCountry = address?.CurrentCountry;
		projection.CurrentPostalCode = address?.CurrentPostalCode;
		projection.PermanentAddress = address?.PermanentAddress;
		projection.PermanentCity = address?.PermanentCity;
		projection.PermanentProvince = address?.PermanentProvince;
		projection.PermanentCountry = address?.PermanentCountry;
		projection.PermanentPostalCode = address?.PermanentPostalCode;

		projection.HighestEducationalAttainment = educational?.HighestEducationalAttainment;
		projection.BachelorsSchoolName = educational?.BachelorsSchoolName;
		projection.BachelorsDegree = educational?.BachelorsDegree;
		projection.MastersSchoolName = educational?.MastersSchoolName;
		projection.MastersDegree = educational?.MastersDegree;
		projection.PhDSchoolName = educational?.PhDSchoolName;
		projection.DoctorateDegree = educational?.DoctorateDegree;

		projection.LicenseName = licenses?.LicenseName;
		projection.LicenseNumber = licenses?.LicenseNumber;
		projection.LicenseExpiryDate = licenses?.LicenseExpiryDate;

		projection.Emp1CompanyName = professional?.Emp1CompanyName;
		projection.Emp1JobTitle = professional?.Emp1JobTitle;
		projection.Emp2CompanyName = professional?.Emp2CompanyName;
		projection.Emp2JobTitle = professional?.Emp2JobTitle;
		projection.Emp3CompanyName = professional?.Emp3CompanyName;
		projection.Emp3JobTitle = professional?.Emp3JobTitle;

		projection.Ref1FullName = reference?.Ref1FullName;
		projection.Ref1ContactNumber = reference?.Ref1ContactNumber;
		projection.Ref2FullName = reference?.Ref2FullName;
		projection.Ref2ContactNumber = reference?.Ref2ContactNumber;
		projection.Ref3FullName = reference?.Ref3FullName;
		projection.Ref3ContactNumber = reference?.Ref3ContactNumber;

		projection.SignerName = signature?.SignerName;
		projection.SignatureDate = signature?.SignatureDate;
		projection.ProjectionUpdatedAt = DateTime.UtcNow;
	}
}
