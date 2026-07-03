namespace PhilSys.Features.InternalSystemQuery;

public record InternalSystemCommand(string callback_url, string inquiry_type, IdentityData identity_data) : ICommand<InternalSystemResult>;
public record InternalSystemResult(PartnerSystemResponseDTO PartnerSystemResponseDTO);
public class InternalSystemCommandValidator : AbstractValidator<InternalSystemCommand>
{
	public InternalSystemCommandValidator()
	{
		RuleFor(x => x.callback_url)
			.NotEmpty().WithMessage("callback_url is required.");

		RuleFor(x => x.inquiry_type)
			.NotEmpty().WithMessage("inquiry_type is required.");

		RuleFor(x => x.identity_data)
			.NotNull().WithMessage("identity_data is required.");
	}
}

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
