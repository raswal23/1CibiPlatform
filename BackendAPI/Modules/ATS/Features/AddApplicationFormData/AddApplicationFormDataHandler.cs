namespace ATS.Features.AddApplicationFormData;

public record AddApplicationFormDataCommand(ApplicationFormDataDTO ApplicationFormDataDTO) : ICommand<AddApplicationFormDataResult>;
public record AddApplicationFormDataResult(bool IsAdded);
public class AddApplicationFormDataHandler : ICommandHandler<AddApplicationFormDataCommand, AddApplicationFormDataResult>
{
	private readonly IATSService _atsService;
	public AddApplicationFormDataHandler(IATSService atsService)
	{
		_atsService = atsService;
	}
	public async Task<AddApplicationFormDataResult> Handle(AddApplicationFormDataCommand request, CancellationToken cancellationToken)
	{
		var result = await _atsService.AddApplicationFormDataAsync(request.ApplicationFormDataDTO, cancellationToken);
		return new AddApplicationFormDataResult(result);
	}
}
