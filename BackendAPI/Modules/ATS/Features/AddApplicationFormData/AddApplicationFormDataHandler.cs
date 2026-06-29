namespace ATS.Features.AddApplicationFormData;

public record AddApplicationFormDataCommand(PersonalDetailsDTO PersonalDetails,
											AddressDetailsDTO AddressDetails, 
											EducationalBackgroundDTO EducationalBackground, 
											LicensesDetailsDTO LicensesDetails, 
											ProfessionalExperiencesDTO ProfessionalExperiences, 
											ReferenceDetailsDTO ReferenceDetails,
											SignatureDetailsDTO SignatureDetails) : ICommand<AddApplicationFormDataResult>;
public record AddApplicationFormDataResult(bool IsAdded);
public class AddApplicationFormDataHandler : ICommandHandler<AddApplicationFormDataCommand, AddApplicationFormDataResult>
{
	private readonly IApplicationFormService _applicationFormService;
	public AddApplicationFormDataHandler(IApplicationFormService applicationFormService)
	{
		_applicationFormService = applicationFormService;
	}
	public async Task<AddApplicationFormDataResult> Handle(AddApplicationFormDataCommand request, CancellationToken cancellationToken)
	{
		var result = await _applicationFormService.AddApplicationFormDataAsync(request.PersonalDetails, 
																			   request.AddressDetails, 
																   request.EducationalBackground, 
																   request.LicensesDetails, 
																   request.ProfessionalExperiences,
																   request.ReferenceDetails,
																   request.SignatureDetails,
																   cancellationToken);
		return new AddApplicationFormDataResult(result);
	}
}
