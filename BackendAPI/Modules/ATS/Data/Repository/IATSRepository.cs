namespace ATS.Data.Repository;

/// <summary>
/// Aggregate repository contract retained for the cache decorator and compatibility.
/// Business services should depend on the focused contracts below instead.
/// </summary>
public interface IATSRepository :
	IApplicantSearchProjectionRepository,
	IApplicationFormRepository,
	IATSUserRepository,
	IBulkUploadRepository,
	IClientRepository,
	IDashboardRepository,
	IDisputeOrderRepository,
	IEmailInvitationRepository,
	IModuleRepository,
	IOrderHistoryRepository,
	IPackageRepository,
	IReportRepository,
	IRoleRepository,
	IUserClientRepository,
	IWithdrawnApplicationRepository
{
}
