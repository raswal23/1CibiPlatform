namespace PhilSys.Services;

public interface IPartnerSystemService
{
	Task<PartnerSystemResponseDTO> PartnerSystemQueryAsync(
		string callback_url,
		string inquiry_type,
		IdentityData identity_data,
		CancellationToken cancellationToken = default);
}
