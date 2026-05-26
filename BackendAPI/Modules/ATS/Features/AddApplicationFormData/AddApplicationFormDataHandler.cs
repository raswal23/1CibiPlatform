using ATS.Services;

namespace ATS.Features.AddApplicationFormData;

public record AddApplicationFormDataCommand(ApplicationFormDataDTO ApplicationFormDataDTO) : ICommand<AddApplicationFormDataResult>;
public record AddApplicationFormDataResult(bool IsAdded);
public class AddApplicationFormDataHandler : ICommandHandler<AddApplicationFormDataCommand, AddApplicationFormDataResult>
{
	private readonly AddApplicationFormDataService _addApplicationFormDataService;
	public AddApplicationFormDataHandler(AddApplicationFormDataService AddApplicationFormDataService)
	{
		_addApplicationFormDataService = AddApplicationFormDataService;
	}
	public async Task<AddApplicationFormDataResult> Handle(AddApplicationFormDataCommand request, CancellationToken cancellationToken)
	{
		var result = await _addApplicationFormDataService.AddApplicationFormDataAsync(request.ApplicationFormDataDTO);
		return new AddApplicationFormDataResult(result);
	}
}
