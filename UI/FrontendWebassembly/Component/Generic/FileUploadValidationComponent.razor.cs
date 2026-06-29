namespace FrontendWebassembly.Component.Generic;

partial class FileUploadValidationComponent
{
	[Parameter] public bool ShowError { get; set; }

	[Parameter]
	public string ErrorText { get; set; } = "This field is required.";
}
