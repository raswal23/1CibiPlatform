namespace PhilSys.Features.InternalSystemQuery;

public record InternalSystemCommand(string callback_url, string inquiry_type, IdentityData identity_data) : ICommand<InternalSystemResult>;
public record InternalSystemResult(PartnerSystemResponseDTO PartnerSystemResponseDTO);
public class InternalSystemCommandValidator : AbstractValidator<InternalSystemCommand>
{
	public InternalSystemCommandValidator()
	{
		RuleFor(x => x.callback_url)
			.NotEmpty().WithMessage("callback_url is required.")
			.Must(url =>
				url == "/" ||
				Uri.IsWellFormedUriString(url, UriKind.Absolute)
			)
			.WithMessage("callback_url must be a valid URL.");

		RuleFor(x => x.inquiry_type)
			.NotEmpty().WithMessage("inquiry_type is required.")
			.Must(t => t == "name_dob" || t == "pcn")
			.WithMessage("inquiry_type must be either 'name_dob' or 'pcn'.");

		RuleFor(x => x.identity_data.ATSSession)
			.NotEmpty().WithMessage("Application Form invitation is required.");

		When(x => x.inquiry_type == "name_dob", () =>
		{
			RuleFor(x => x.identity_data.FirstName)
				.NotEmpty().WithMessage("First Name is required for 'name_dob' inquiry.")
				.MaximumLength(100).WithMessage("First Name must not exceed 100 characters.");

			RuleFor(x => x.identity_data.MiddleName)
				.MaximumLength(100).WithMessage("Middle Name must not exceed 100 characters.");

			RuleFor(x => x.identity_data.LastName)
				.NotEmpty().WithMessage("Last Name is required for 'name_dob' inquiry.")
				.MaximumLength(100).WithMessage("Last Name must not exceed 100 characters.");

			RuleFor(x => x.identity_data.Suffix)
				.MaximumLength(20).WithMessage("Suffix must not exceed 20 characters.");

			RuleFor(x => x.identity_data.BirthDate)
				.NotEmpty().WithMessage("Birth Date is required for 'name_dob' inquiry.")
				.Matches(@"^\d{4}-\d{2}-\d{2}$")
				.WithMessage("Birth Date must be in format yyyy-MM-dd.");
		});

		When(x => x.inquiry_type == "pcn", () =>
		{
			RuleFor(x => x.identity_data.PCN)
				.NotEmpty().WithMessage("PCN is required for 'pcn' inquiry.")
				.Matches(@"^\d{16}$")
				.WithMessage("PCN must be exactly 16 digits.");
		});
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
