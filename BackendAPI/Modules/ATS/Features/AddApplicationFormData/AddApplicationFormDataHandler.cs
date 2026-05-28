namespace ATS.Features.AddApplicationFormData;

public record AddApplicationFormDataCommand(PersonalDetailsDTO PersonalDetails,
											AddressDetailsDTO AddressDetails, 
											EducationalBackgroundDTO EducationalBackground, 
											LicensesDetailsDTO LicensesDetails, 
											ProfessionalExperiencesDTO ProfessionalExperiences, 
											ReferenceDetailsDTO ReferenceDetails) : ICommand<AddApplicationFormDataResult>;
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
		var result = await _atsService.AddApplicationFormDataAsync(request.PersonalDetails, 
																   request.AddressDetails, 
																   request.EducationalBackground, 
																   request.LicensesDetails, 
																   request.ProfessionalExperiences,
																   request.ReferenceDetails,
																   cancellationToken);
		return new AddApplicationFormDataResult(result);
	}
}
