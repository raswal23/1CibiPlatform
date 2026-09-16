# OMS Auto-Ticketing — Updated Mapping Documentation

## Government ID Handling (SSS and TIN Numbers)

### Current Mapping Implementation

The OMS auto-ticketing system handles government identification numbers (SSS and TIN) through the `OMSTicketPayloadMapper.NormalizeGovernmentId` method in `BackendAPI/Modules/ATS/Services/OMSTicketing/OMSTicketPayloadMapper.cs`.

### Normalization Process

The `NormalizeGovernmentId` method performs the following steps:

1. **Input Validation**: Checks if the value is null or whitespace and returns null if so
2. **Character Filtering**: Strips all non-digit characters, retaining only numeric digits
3. **Length Validation**: Verifies the resulting digit string matches the required length:
   - SSS Number: Must be exactly 10 digits
   - TIN Number: Must be between 9-12 digits
4. **Output**: Returns the cleaned digit string if valid, otherwise returns null

```csharp
public static string? NormalizeGovernmentId(string? value, int requiredLength)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    var digits = new string(value.Where(char.IsDigit).ToArray());

    return digits.Length == requiredLength
        ? digits
        : null;
}
```

### Integration Behavior

- **SSS Numbers**: Passed through as `NormalizeGovernmentId(payload.SSS, 10)`
- **TIN Numbers**: Passed through as `NormalizeGovernmentId(payload.TIN, 12)`
- **Purpose**: The field is optional, so malformed values are sent as blank rather than failing the entire ticket

### Data Quality Layer

While the OMS mapping tolerates various input formats by normalizing them, the ATS validation layer ensures data quality at entry points:

- **Bulk Upload Validation**: Rejects entries with special characters or incorrect lengths
- **Form Validation**: Enforces clean data entry standards
- **Error Messages**: Provides clear feedback about requirements (numeric digits only, specific lengths)

### Design Rationale

This two-tier approach ensures:
1. **Data Entry Quality**: Clean, standardized data at the source
2. **Integration Flexibility**: Compatibility with external systems that may send varied formats
3. **Error Prevention**: Proper validation prevents malformed data from reaching OMS

### Requirements

For optimal processing, input data should meet these standards:
- SSS Number: Exactly 10 numeric digits (no special characters)
- TIN Number: 9-12 numeric digits (no special characters)
- Date of Birth: MM/dd/yyyy format (for data screening orders)

This ensures both clean data storage in ATS and reliable transmission to OMS systems.