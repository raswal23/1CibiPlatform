namespace FrontendWebassembly.Services.Auth.Shared;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class RequireATSModuleAttribute : Attribute
{
	public IReadOnlyCollection<int> ModuleIds { get; }

	public RequireATSModuleAttribute(params int[] moduleIds)
	{
		ModuleIds = moduleIds;
	}
}
