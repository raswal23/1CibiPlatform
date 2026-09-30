using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ATS.Data.Interceptors;

/// <summary>
/// Captures the before/after value of every property an ATS command changed, so the audit
/// trail can answer "what did they change it from" rather than only "they edited this".
///
/// Reads the change tracker in <c>SavingChanges</c>, not <c>SavedChanges</c>: once the
/// save completes EF marks entries Unchanged and the original values are gone.
///
/// This sees only <b>tracked</b> writes - the load-then-mutate-then-save pattern the
/// settings services use. <c>ExecuteUpdateAsync</c> issues SQL directly and never
/// populates the change tracker, so those commands record no field changes and the UI
/// says so rather than implying nothing changed.
/// </summary>
public sealed class AtsAuditChangeInterceptor : SaveChangesInterceptor
{
	private readonly IAtsAuditChangeCollector _collector;

	public AtsAuditChangeInterceptor(IAtsAuditChangeCollector collector) =>
		_collector = collector;

	public override InterceptionResult<int> SavingChanges(
		DbContextEventData eventData,
		InterceptionResult<int> result)
	{
		CaptureAsync(eventData.Context, CancellationToken.None)
			.GetAwaiter()
			.GetResult();

		return base.SavingChanges(eventData, result);
	}

	public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
		DbContextEventData eventData,
		InterceptionResult<int> result,
		CancellationToken cancellationToken = default)
	{
		await CaptureAsync(eventData.Context, cancellationToken);

		return await base.SavingChangesAsync(eventData, result, cancellationToken);
	}

	private async Task CaptureAsync(DbContext? context, CancellationToken cancellationToken)
	{
		if (context is null)
		{
			return;
		}

		var changes = new List<AtsEntityChangeDTO>();

		foreach (var entry in context.ChangeTracker.Entries().ToList())
		{
			// The audit table is written by the drain on its own context, but guard anyway
			// so an audit entry can never describe itself.
			if (entry.Entity is AtsAuditEntry)
			{
				continue;
			}

			if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
			{
				continue;
			}

			// A detached update - DbSet.Update() on an AsNoTracking() entity, which is how
			// the package, role and module repositories save - has no usable originals:
			// EF marks every property modified with OriginalValue equal to CurrentValue.
			// The stored row is then the only source of the before values, so it is read
			// back before the UPDATE overwrites it.
			if (entry.State == EntityState.Modified && !HasUsableOriginals(entry))
			{
				await LoadOriginalsAsync(entry, cancellationToken);
			}

			var change = Describe(entry);

			// A Modified entry whose properties all happen to match is not worth a row.
			if (change.State == nameof(EntityState.Modified) && change.Changes.Count == 0)
			{
				continue;
			}

			changes.Add(change);
		}

		if (changes.Count > 0)
		{
			_collector.Add(changes);
		}
	}

	// True when the tracker holds a real before-image: at least one modified property
	// whose original differs from its current value. A genuinely tracked edit always
	// does; a detached Update() never does.
	private static bool HasUsableOriginals(EntityEntry entry) =>
		entry.Properties.Any(property =>
			property.IsModified && !Equals(property.OriginalValue, property.CurrentValue));

	private static async Task LoadOriginalsAsync(
		EntityEntry entry,
		CancellationToken cancellationToken)
	{
		try
		{
			// Reads the row as it stands in the database and copies it over the entry's
			// original values, so Describe below compares against what was really there.
			// One extra SELECT per detached edit, on the write path only.
			var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);

			if (databaseValues is not null)
			{
				entry.OriginalValues.SetValues(databaseValues);
			}
		}
		catch (Exception)
		{
			// The row may have been deleted concurrently, or the provider may not support
			// the lookup. The diff is then merely incomplete - it must not fail the save.
		}
	}

	private static AtsEntityChangeDTO Describe(EntityEntry entry)
	{
		var change = new AtsEntityChangeDTO
		{
			Entity = entry.Metadata.ClrType.Name,
			Key = DescribeKey(entry),
			State = entry.State.ToString()
		};

		foreach (var property in entry.Properties)
		{
			var name = property.Metadata.Name;

			// A sensitive column is still reported as having changed - that an SMTP password
			// was replaced is exactly what an audit reader needs - but neither value is kept.
			// The names come from AtsAuditRedactor so this and the command payloads mask the
			// same set; a second list here would drift and leak without anything noticing.
			var isSensitive = AtsAuditRedactor.IsSensitiveProperty(name);

			switch (entry.State)
			{
				// A create has no before value; recording "null -> x" for every column
				// would bury the few fields that matter, so only non-defaults are kept.
				case EntityState.Added when property.CurrentValue is not null:
					change.Changes[name] = new AtsPropertyChangeDTO(
						null,
						isSensitive ? AtsAuditRedactor.Mask : Format(property.CurrentValue));
					break;

				// Compared by value rather than trusting IsModified: a detached Update()
				// flags every property, so IsModified alone would report the whole row as
				// changed once the originals above are loaded.
				case EntityState.Modified
					when !Equals(property.OriginalValue, property.CurrentValue):
					change.Changes[name] = isSensitive
						? new AtsPropertyChangeDTO(AtsAuditRedactor.Mask, AtsAuditRedactor.Mask)
						: new AtsPropertyChangeDTO(
							Format(property.OriginalValue),
							Format(property.CurrentValue));
					break;

				// A delete has no after value. The original is what is worth keeping -
				// it is the only remaining record of the row.
				case EntityState.Deleted when property.OriginalValue is not null:
					change.Changes[name] = new AtsPropertyChangeDTO(
						isSensitive ? AtsAuditRedactor.Mask : Format(property.OriginalValue),
						null);
					break;
			}
		}

		return change;
	}

	private static string? DescribeKey(EntityEntry entry)
	{
		var keyValues = entry.Metadata.FindPrimaryKey()?.Properties
			.Select(property => Format(entry.Property(property.Name).CurrentValue))
			.Where(value => value is not null)
			.ToArray();

		// Composite keys - UserDetails is (UserId, ModuleId) - are joined so the row is
		// still identifiable from one string.
		return keyValues is { Length: > 0 }
			? string.Join(" / ", keyValues)
			: null;
	}

	// Everything becomes a string: the dialog renders these as text, and a single column
	// type keeps the stored shape stable whatever the property was.
	private static string? Format(object? value) => value switch
	{
		null => null,
		DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
		DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
		DateOnly dateOnly => dateOnly.ToString("O", CultureInfo.InvariantCulture),
		IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
		_ => value.ToString()
	};
}
