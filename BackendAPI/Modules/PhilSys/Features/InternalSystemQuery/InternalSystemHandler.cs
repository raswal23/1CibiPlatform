namespace PhilSys.Features.InternalSystemQuery;

public record InternalSystemCommand(string callback_url, string inquiry_type, IdentityData identity_data) : ICommand<InternalSystemResult>;
public record InternalSystemResult(PartnerSystemResponseDTO PartnerSystemResponseDTO);
public class InternalSystemHandler : ICommandHandler<InternalSystemCommand, InternalSystemResult>
{
	private readonly PartnerSystemService _partnerSystemService;

	public InternalSystemHandler(PartnerSystemService PartnerSystemService)
	{
		_partnerSystemService = PartnerSystemService;
	}
	public async Task<InternalSystemResult> Handle(InternalSystemCommand request, CancellationToken cancellationToken)
	{
		var result = await _partnerSystemService.PartnerSystemQueryAsync(request.callback_url, request.inquiry_type, request.identity_data);
		return new InternalSystemResult(result);
	}
}
