namespace FrontendWebassembly.Pages.Philsys;

public partial class PhilSysLiveness
{
	[Parameter]
	public required string HashToken { get; set; }
	private UpdateFaceLivenessSessionResponseDTO? information;
	private TransactionStatusResponseDTO? _status;
	private bool _completed = false;
	private bool isTransacted = false;
	private bool isExpired = false;
	private bool showLoader = false;
	private string? webHookUrl;
	private string? livenessKey;
	private string errorMessage = string.Empty;
	private bool hasUnsavedChanges = true;
	private DotNetObjectReference<PhilSysLiveness>? _dotNetRef;
	public string? atsSession { get; set; } 
	public string? applicationFormPath { get; set; }

	protected override async Task OnInitializedAsync()
	{
		_status = await IPhilSysService.GetTransactionStatusAsync(HashToken);
		atsSession = _status.ATSSession;
		applicationFormPath = _status.ATSApplicationFormPath;
		webHookUrl = _status.WebHookUrl;
		isTransacted = _status.IsTransacted;
		isExpired = _status.isExpired;

		if(string.IsNullOrEmpty(atsSession))
		{
			hasUnsavedChanges = false;
		}

		if (_status.isExpired)
		{
			await IPhilSysService.DeleteTransactionAsync(HashToken);
			return;
		}
	}
	private async Task ConfirmNavigation(LocationChangingContext context)
	{
		if (hasUnsavedChanges)
		{
			var result = await JS.InvokeAsync<bool>("confirm", "You have unsaved changes. Leave anyway?");

			if (!result)
			{
				context.PreventNavigation();
			}
		}
	}

	private async Task StartLiveness()
	{
		_status = await IPhilSysService.GetTransactionStatusAsync(HashToken);

		if (_status.isExpired)
		{
			isExpired = true;
			await IPhilSysService.DeleteTransactionAsync(HashToken);
			StateHasChanged();
			return;
		}

		if (_status.IsTransacted)
		{
			isTransacted = true;
			StateHasChanged();
			return;
		}

		livenessKey = await IPhilSysService.GetLivenessKeyAsync();

		if (string.IsNullOrEmpty(livenessKey))
		{
			_completed = true;
			StateHasChanged();
			return;
		}

		_dotNetRef = DotNetObjectReference.Create(this);
		await JS.InvokeVoidAsync("startLivenessInterop", HashToken, _dotNetRef, livenessKey);
	}

	[JSInvokable]
	public async Task OnLivenessCompleted(string sessionId)
	{
		showLoader = true;
		StateHasChanged();

		information = await IPhilSysService.UpdateFaceLivenessSessionAsync(HashToken, sessionId);
		_completed = true;

		if (!string.IsNullOrEmpty(information.error_message))
		{
			errorMessage = information.error_message!;
		}

		showLoader = false;
		hasUnsavedChanges = false;
		if (!string.IsNullOrEmpty(atsSession))
		{
			if(information.data_subject is not null)
			{
				await LocalStorageService.SetItemAsync($"ats:applicationForm:digitalId", information.data_subject!.digital_id ?? string.Empty);
				await LocalStorageService.SetItemAsync($"ats:applicationForm:firstName", information.data_subject!.first_name ?? string.Empty);
				await LocalStorageService.SetItemAsync($"ats:applicationForm:middleName", information.data_subject!.middle_name ?? string.Empty);
				await LocalStorageService.SetItemAsync($"ats:applicationForm:lastName", information.data_subject!.last_name ?? string.Empty);
				await LocalStorageService.SetItemAsync($"ats:applicationForm:suffix", information.data_subject!.suffix ?? string.Empty);
				await LocalStorageService.SetItemAsync($"ats:applicationForm:birthDate", information.data_subject!.birth_date ?? string.Empty);
				await LocalStorageService.SetItemAsync($"ats:applicationForm:sex", information.data_subject!.gender ?? string.Empty);
				await LocalStorageService.SetItemAsync($"ats:applicationForm:emailAddress", information.data_subject!.email ?? string.Empty);
				await LocalStorageService.SetItemAsync($"ats:applicationForm:phoneNumber", information.data_subject!.mobile_number ?? string.Empty);
				await LocalStorageService.SetItemAsync($"ats:applicationForm:profilePicture", information.data_subject!.face_url ?? string.Empty);
			}

			Navigation.NavigateTo($"{applicationFormPath}/{atsSession}?showAppForm=true&philSysShow=false&stepActive=1", false);

			return;
		}

		StateHasChanged();
	}

	public async ValueTask DisposeAsync()
	{
		_dotNetRef?.Dispose();
	}
}
