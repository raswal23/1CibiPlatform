using Microsoft.AspNetCore.Components;

namespace FrontendWebassembly.Component.Generic;

public partial class LoaderComponent
{
	[Parameter] public string Height { get; set; } = "100vh";
}
