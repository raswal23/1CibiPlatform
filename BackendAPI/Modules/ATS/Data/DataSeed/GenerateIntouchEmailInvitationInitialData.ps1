[CmdletBinding()]
param(
	[string]$ExcelPath = 'F:\CIBI\ATS\modifiedIntouch.xlsx',
	[string]$JsonPath = (Join-Path $PSScriptRoot 'IntouchEmailInvitationInitialData.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-NormalizedHeader([string]$Header) {
	return ($Header -replace '[^A-Za-z0-9]', '').ToLowerInvariant()
}

function Test-MissingValue($Value) {
	return $null -eq $Value -or ($Value -is [string] -and [string]::IsNullOrWhiteSpace($Value))
}

function Get-StableBytes([string]$Seed) {
	$sha = [System.Security.Cryptography.SHA256]::Create()
	try {
		return $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Seed))
	}
	finally {
		$sha.Dispose()
	}
}

function Get-StableNumber([string]$Seed, [int]$Minimum, [int]$MaximumExclusive) {
	if ($MaximumExclusive -le $Minimum) {
		return $Minimum
	}

	$bytes = Get-StableBytes $Seed
	$number = [BitConverter]::ToUInt32($bytes, 0)
	return $Minimum + [int]($number % [uint32]($MaximumExclusive - $Minimum))
}

function Get-StableGuid([string]$Seed) {
	$bytes = Get-StableBytes $Seed
	$guidBytes = [byte[]]::new(16)
	[Array]::Copy($bytes, $guidBytes, 16)
	$guidBytes[7] = [byte](($guidBytes[7] -band 0x0F) -bor 0x50)
	$guidBytes[8] = [byte](($guidBytes[8] -band 0x3F) -bor 0x80)
	return ([Guid]::new($guidBytes)).ToString()
}

function Select-StableValue([string]$Seed, [object[]]$Values) {
	return $Values[(Get-StableNumber $Seed 0 $Values.Count)]
}

function Convert-ToSlug([string]$Value) {
	$slug = $Value.ToLowerInvariant() -replace '[^a-z0-9]+', '.'
	return $slug.Trim('.')
}

function Convert-ToDate($Value, [datetime]$Fallback) {
	if ($Value -is [double] -or $Value -is [int]) {
		return [datetime]::FromOADate([double]$Value)
	}

	$date = [datetime]::MinValue
	if ([datetime]::TryParse(
		[string]$Value,
		[Globalization.CultureInfo]::InvariantCulture,
		[Globalization.DateTimeStyles]::AssumeLocal,
		[ref]$date)) {
		return $date
	}

	if ([string]$Value -match '^\s*(\d{1,2})/(\d{1,2})/(\d{4})') {
		$month = [int]$Matches[1]
		$day = [int]$Matches[2]
		$year = [int]$Matches[3]
		return ([datetime]::new($year, $month, 1)).AddDays($day - 1)
	}

	return $Fallback
}

function Convert-ToIsoDateTime([datetime]$Value) {
	return $Value.ToUniversalTime().ToString(
		'yyyy-MM-ddTHH:mm:ss.fffZ',
		[Globalization.CultureInfo]::InvariantCulture)
}

function Convert-ToIsoDate([datetime]$Value) {
	return $Value.ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
}

function Get-ScalarEntityProperties([string]$EntityName) {
	$entityPath = Join-Path (Join-Path $PSScriptRoot '..\Entities') "$EntityName.cs"
	if (-not (Test-Path -LiteralPath $entityPath)) {
		throw "ATS entity source was not found: $entityPath"
	}

	$properties = [Collections.Generic.List[object]]::new()
	foreach ($line in Get-Content -LiteralPath $entityPath) {
		if ($line -notmatch '^\s*public\s+(?<Type>[A-Za-z0-9_<>?]+)\s+(?<Name>[A-Za-z0-9_]+)\s*\{\s*get;\s*set;') {
			continue
		}

		$type = $Matches.Type
		$name = $Matches.Name
		if ($type -like 'ICollection*' -or $type -like 'EmailInvitationRequest*') {
			continue
		}

		$properties.Add([pscustomobject]@{ Type = $type; Name = $name })
	}

	return $properties
}

function Get-SourceValue($Context, [string]$Header) {
	$normalized = Get-NormalizedHeader $Header
	if ($Context.SourceValues.ContainsKey($normalized)) {
		return $Context.SourceValues[$normalized]
	}

	return $null
}

function Get-CompletedValue($Context, [string]$Path) {
	if ($Context.CompletedValues.ContainsKey($Path)) {
		return $Context.CompletedValues[$Path]
	}

	return $null
}

function Get-RandomPersonName([string]$Seed) {
	$firstNames = @(
		'Adrian', 'Andrea', 'Angela', 'Bianca', 'Carlo', 'Christine', 'Daniel',
		'Elena', 'Gabriel', 'Isabel', 'Joshua', 'Katrina', 'Marco', 'Patricia',
		'Rafael', 'Samantha', 'Sofia', 'Tristan', 'Vanessa', 'Vincent')
	$lastNames = @(
		'Aguilar', 'Bautista', 'Castillo', 'Cruz', 'Del Rosario', 'Domingo',
		'Flores', 'Garcia', 'Gonzales', 'Hernandez', 'Lim', 'Mendoza', 'Navarro',
		'Pascual', 'Reyes', 'Rivera', 'Santos', 'Torres', 'Valdez', 'Villanueva')
	return "$(Select-StableValue "${Seed}:first" $firstNames) $(Select-StableValue "${Seed}:last" $lastNames)"
}

function Get-Location([string]$Seed) {
	$locations = @(
		@('Makati City', 'Metro Manila', '1226'),
		@('Quezon City', 'Metro Manila', '1104'),
		@('Pasig City', 'Metro Manila', '1605'),
		@('Taguig City', 'Metro Manila', '1634'),
		@('Cebu City', 'Cebu', '6000'),
		@('Davao City', 'Davao del Sur', '8000'),
		@('Baguio City', 'Benguet', '2600'),
		@('Iloilo City', 'Iloilo', '5000'),
		@('Bacolod City', 'Negros Occidental', '6100'),
		@('Cagayan de Oro City', 'Misamis Oriental', '9000'))
	return $locations[(Get-StableNumber $Seed 0 $locations.Count)]
}

function Get-BaseGeneratedValue($Context, [string]$PropertyName, [string]$Type) {
	$ticket = $Context.TicketNo
	$subjectName = [string](Get-SourceValue $Context 'Subject Name')
	$nameParts = $subjectName -split ',', 2 | ForEach-Object { $_.Trim() }
	$lastName = if ($nameParts.Count -gt 0) { $nameParts[0] } else { 'Santos' }
	$firstName = if ($nameParts.Count -gt 1) { $nameParts[1] } else { Get-RandomPersonName "${ticket}:subject" }
	$orderCreated = Convert-ToDate (Get-SourceValue $Context 'Date Endorsed') ([datetime]::UtcNow.Date)
	$orderCompleted = Convert-ToDate (Get-SourceValue $Context 'Released Date') ($orderCreated.AddDays(5))
	$reportUploaded = Convert-ToDate (Get-SourceValue $Context 'Ticket Date') $orderCreated
	$seconds = Get-StableNumber "${ticket}:report-time" (9 * 3600) (17 * 3600)
	$reportUploaded = $reportUploaded.Date.AddSeconds($seconds)

	switch ($PropertyName) {
		'EmailInvitationId' { return Get-StableGuid "intouch:$ticket" }
		'TicketNo' { return $ticket }
		'FirstName' { return $firstName }
		'LastName' { return $lastName }
		'SelectPackage' { return [string](Get-SourceValue $Context 'Report Type') }
		'RushNormal' { return [string](Get-SourceValue $Context 'TAT') }
		'ClientId' { return [int](Get-SourceValue $Context 'ClientID') }
		'OrderStatus' { return [string](Get-SourceValue $Context 'Ticket Status') }
		'OrderCreatedAt' { return Convert-ToIsoDateTime $orderCreated }
		'OrderCompletedAt' { return Convert-ToIsoDateTime $orderCompleted }
		'HitStatus' { return [string](Get-SourceValue $Context 'Hit status') }
		'ReportStatus' { return [string](Get-SourceValue $Context 'Report Status') }
		'ReportUploadedAt' { return Convert-ToIsoDateTime $reportUploaded }
		'Requestor' { return [string](Get-SourceValue $Context 'Requestor') }
		default { throw "No base mapping exists for '$PropertyName'." }
	}
}

function Get-RelatedGeneratedValue($Context, $Mapping) {
	$ticket = $Context.TicketNo
	$property = $Mapping.PropertyName
	$entity = $Mapping.EntityName
	$type = $Mapping.Type
	$seed = "${ticket}:$($Mapping.Path)"
	$emailInvitationId = [string](Get-CompletedValue $Context 'EmailInvitationId')
	$firstName = [string](Get-CompletedValue $Context 'FirstName')
	$lastName = [string](Get-CompletedValue $Context 'LastName')
	$fullName = "$firstName $lastName".Trim()
	$orderCreated = Convert-ToDate (Get-CompletedValue $Context 'OrderCreatedAt') ([datetime]::UtcNow.Date)
	$orderCompleted = Convert-ToDate (Get-CompletedValue $Context 'OrderCompletedAt') ($orderCreated.AddDays(5))
	$reportUploaded = Convert-ToDate (Get-CompletedValue $Context 'ReportUploadedAt') $orderCompleted
	$location = Get-Location "${ticket}:location"

	if ($entity -eq 'ApplicantSearchProjection') {
		$reusePaths = @(
			$property,
			"PersonalDetails.$property",
			"AddressDetails.$property",
			"EducationalBackground.$property",
			"LicensesDetails.$property",
			"ProfessionalExperiences.$property",
			"ReferenceDetails.$property",
			"SignatureDetails.$property")
		foreach ($reusePath in $reusePaths) {
			$reuse = Get-CompletedValue $Context $reusePath
			if (-not (Test-MissingValue $reuse)) {
				return $reuse
			}
		}
	}

	if ($property -in @(
			'EmailInvitationID',
			'EmailInvitationRequestId')) {
		return $emailInvitationId
	}

	if ($type -like 'Guid*') {
		if ($property -eq 'ChangedByUserId') {
			return Get-StableGuid "${ticket}:changed-by"
		}
		return Get-StableGuid $seed
	}

	if ($type -eq 'bool' -or $type -eq 'bool?') {
		return (Get-StableNumber $seed 0 2) -eq 1
	}

	if ($property -eq 'CreatedDate') { return Convert-ToIsoDateTime $orderCreated }
	if ($property -eq 'ProjectionUpdatedAt') { return Convert-ToIsoDateTime $orderCompleted }
	if ($property -eq 'ReportUploadedAt') {
		if ($entity -eq 'ArchiveReport') { return Convert-ToIsoDateTime ($reportUploaded.AddDays(-2)) }
		return Convert-ToIsoDateTime $reportUploaded
	}
	if ($property -eq 'OccurredAt') { return Convert-ToIsoDateTime $orderCompleted }
	if ($property -eq 'ConsentGeneratedAt') { return Convert-ToIsoDateTime $orderCompleted }
	if ($property -eq 'Ref1BestTimeToContact' -or
		$property -eq 'Ref2BestTimeToContact' -or
		$property -eq 'Ref3BestTimeToContact') {
		return Convert-ToIsoDateTime ($orderCreated.Date.AddHours(9 + (Get-StableNumber $seed 0 8)))
	}

	if ($type -like 'DateOnly*') {
		if ($property -eq 'DOB') {
			return Convert-ToIsoDate ($orderCreated.AddYears(-(Get-StableNumber $seed 22 46)).AddDays(-(Get-StableNumber "${seed}:day" 0 365)))
		}
		if ($property -like '*GraduationDate') {
			$yearsBack = if ($property -like '*HighSchool*') { 10 } elseif ($property -like '*Masters*') { 4 } elseif ($property -like '*Doctorate*') { 2 } else { 7 }
			return Convert-ToIsoDate ($orderCreated.AddYears(-$yearsBack).AddDays(-(Get-StableNumber $seed 0 180)))
		}
		if ($property -eq 'LicenseExpiryDate') { return Convert-ToIsoDate ($orderCreated.AddYears(3)) }
		if ($property -eq 'SignatureDate') { return Convert-ToIsoDate $orderCreated }
		if ($property -like 'Emp*StartDate') { return Convert-ToIsoDate ($orderCreated.AddYears(-(Get-StableNumber $seed 2 8))) }
		if ($property -like 'Emp*EndDate') { return Convert-ToIsoDate ($orderCreated.AddMonths(-(Get-StableNumber $seed 1 18))) }
		return Convert-ToIsoDate $orderCreated
	}

	if ($type -like 'DateTime*') {
		return Convert-ToIsoDateTime $orderCreated
	}

	if ($property -in @('FirstName', 'LastName')) { return Get-CompletedValue $Context $property }
	if ($property -eq 'MiddleInitial') { return (Select-StableValue $seed @('A', 'C', 'D', 'G', 'L', 'M', 'P', 'R', 'S', 'T')) }
	if ($property -eq 'MiddleName') { return (Get-RandomPersonName $seed).Split(' ')[0] }
	if ($property -eq 'SignerName') { return $fullName }
	if ($property -like 'Ref?FullName' -or $property -like 'Emp?SupervisorName') { return Get-RandomPersonName $seed }
	if ($property -like '*EmailAlternative') {
		return "$(Convert-ToSlug $fullName).alt.$(Get-StableNumber $seed 10 1000)@example.com"
	}
	if ($property -like '*EmailAddress') {
		return "$(Convert-ToSlug $fullName).$(Get-StableNumber $seed 10 1000)@example.com"
	}
	if ($property -like 'Ref?Email' -or $property -like 'Emp?SupervisorEmail') {
		$name = Get-RandomPersonName $seed
		return "$(Convert-ToSlug $name).$(Get-StableNumber $seed 10 1000)@example.com"
	}
	if ($property -like '*MobileNumber' -or $property -like '*ContactNumber') {
		return "09$((Get-StableNumber $seed 100000000 999999999).ToString('000000000'))"
	}
	if ($property -eq 'TelephoneNumber') { return "02-8$((Get-StableNumber $seed 1000000 9999999).ToString('0000000'))" }
	if ($property -eq 'PositionAppliedFor') { return Select-StableValue $seed @('Data Analyst', 'Customer Service Specialist', 'Software Engineer', 'Operations Associate', 'Finance Analyst', 'Human Resources Specialist') }
	if ($property -eq 'Suffix') { return Select-StableValue $seed @('N/A', 'Jr.', 'III') }
	if ($property -eq 'MaritalStatus') { return Select-StableValue $seed @('Single', 'Married', 'Single', 'Single') }
	if ($property -eq 'Nationality') { return 'Filipino' }
	if ($property -eq 'Sex') { return Select-StableValue $seed @('Female', 'Male') }
	if ($property -eq 'SSS') { return "$((Get-StableNumber $seed 10 99).ToString('00'))-$((Get-StableNumber "${seed}:a" 1000000 9999999).ToString('0000000'))-$((Get-StableNumber "${seed}:b" 0 9))" }
	if ($property -eq 'TIN') { return "$((Get-StableNumber $seed 100 999))-$((Get-StableNumber "${seed}:a" 100 999))-$((Get-StableNumber "${seed}:b" 100 999))-000" }
	if ($property -like '*TypeOfOwnership') { return Select-StableValue $seed @('Owned', 'Rented', 'Living with family') }
	if ($property -like '*City') { return $location[0] }
	if ($property -like '*Province') { return $location[1] }
	if ($property -like '*Country') { return 'Philippines' }
	if ($property -like '*PostalCode') { return $location[2] }
	if ($property -like '*Address') {
		return "$(Get-StableNumber $seed 10 999) $(Select-StableValue "${seed}:street" @('Acacia Street', 'Mabini Avenue', 'Rizal Street', 'Sampaguita Road', 'Bonifacio Drive')), $($location[0])"
	}
	if ($property -eq 'CurrentStayFrom') { return "$(Get-StableNumber $seed 2 12) years" }
	if ($property -eq 'HighestEducationalAttainment') { return Select-StableValue $seed @("Bachelor's Degree", "Bachelor's Degree", "Master's Degree", 'College Graduate') }
	if ($property -like '*SchoolName') { return Select-StableValue $seed @('University of the Philippines', 'Polytechnic University of the Philippines', 'De La Salle University', 'Ateneo de Manila University', 'University of San Carlos', 'Far Eastern University') }
	if ($property -like '*Degree') { return Select-StableValue $seed @('Bachelor of Science', 'Bachelor of Arts', 'Master of Business Administration', 'Doctor of Philosophy') }
	if ($property -like '*Major') { return Select-StableValue $seed @('Information Technology', 'Business Administration', 'Psychology', 'Accountancy', 'Communication') }
	if ($property -eq 'LicenseName') { return Select-StableValue $seed @('Civil Service Professional Eligibility', 'Licensed Professional Teacher', 'Certified Public Accountant', 'Professional Driver License') }
	if ($property -eq 'LicenseNumber') { return "LIC-$((Get-StableNumber $seed 1000000 9999999))" }
	if ($property -like 'Emp?CompanyName' -or $property -like 'Ref?AffiliatedCompany') { return Select-StableValue $seed @('Ayala Corporation', 'SM Investments Corporation', 'Jollibee Foods Corporation', 'Globe Telecom', 'San Miguel Corporation', 'BDO Unibank') }
	if ($property -like 'Emp?CurrentlyEmployed') { return Select-StableValue $seed @('No', 'No', 'Yes') }
	if ($property -like 'Emp?PermissionToContact') { return 'Yes' }
	if ($property -like 'Emp?JobTitle') { return Select-StableValue $seed @('Team Lead', 'Business Analyst', 'Operations Associate', 'Account Specialist', 'Software Developer', 'Administrative Officer') }
	if ($property -like 'Emp?ReasonForLeaving') { return Select-StableValue $seed @('Career advancement', 'End of contract', 'Relocation', 'Better opportunity') }
	if ($property -like 'Ref?ProfessionalRelationship') { return Select-StableValue $seed @('Former Supervisor', 'Colleague', 'Team Lead', 'Department Manager') }
	if ($property -like 'Ref?ModeOfContact') { return Select-StableValue $seed @('Mobile', 'Email', 'Mobile') }
	if ($property -eq 'DocumentName') { return Select-StableValue $seed @('Government ID', 'Resume', 'NBI Clearance', 'Diploma') }
	if ($property -eq 'DocumentValue') { return "documents/$ticket/$(Convert-ToSlug (Select-StableValue $seed @('government-id', 'resume', 'nbi-clearance', 'diploma'))).pdf" }
	if ($property -eq 'HitStatus') { return Get-CompletedValue $Context 'HitStatus' }
	if ($property -eq 'ReportStatus') {
		if ($entity -eq 'ArchiveReport') { return 'Initial Report' }
		return Get-CompletedValue $Context 'ReportStatus'
	}
	if ($property -eq 'EventType') { return 'StatusChanged' }
	if ($property -eq 'PreviousStatus') { return 'In Progress' }
	if ($property -eq 'NewStatus') { return Get-CompletedValue $Context 'OrderStatus' }
	if ($property -eq 'Source') { return 'InitialData' }
	if ($property -eq 'ApplicationFormStatus') { return 'Done' }
	if ($property -eq 'SelectPackage') { return Get-CompletedValue $Context 'SelectPackage' }
	if ($property -eq 'RushNormal') { return Get-CompletedValue $Context 'RushNormal' }
	if ($property -eq 'OrderStatus') { return Get-CompletedValue $Context 'OrderStatus' }
	if ($property -eq 'OrderCreatedAt') { return Get-CompletedValue $Context 'OrderCreatedAt' }
	if ($property -eq 'OrderCompletedAt') { return Get-CompletedValue $Context 'OrderCompletedAt' }
	if ($property -like '*FileName') {
		$base = ($property -replace 'FileName$', '')
		return "$(Convert-ToSlug $base)-$ticket.pdf"
	}
	if ($property -like '*FileKey') {
		$base = ($property -replace 'FileKey$', '')
		return "seed/$ticket/$(Convert-ToSlug $base)-$ticket.pdf"
	}

	return "$property $(Get-StableNumber $seed 1000 9999)"
}

function Convert-CompletedValueForJson($Value, [string]$Type) {
	if ($Type -eq 'int' -or $Type -eq 'int?') { return [int]$Value }
	if ($Type -eq 'long' -or $Type -eq 'long?') { return [long]$Value }
	if ($Type -eq 'bool' -or $Type -eq 'bool?') {
		if ($Value -is [bool]) { return $Value }
		return [Convert]::ToBoolean($Value, [Globalization.CultureInfo]::InvariantCulture)
	}
	if ($Value -is [double] -and $Type -like 'Date*') {
		$date = [datetime]::FromOADate($Value)
		return if ($Type -like 'DateOnly*') { Convert-ToIsoDate $date } else { Convert-ToIsoDateTime $date }
	}
	return [string]$Value
}

function Add-CompletedPath($Target, [string]$Path, $Value) {
	$parts = $Path -split '\.'
	if ($parts.Count -eq 1) {
		$Target[$parts[0]] = $Value
		return
	}

	$containerName = $parts[0]
	$propertyName = $parts[1]
	if ($containerName -match '^(?<Name>[^\[]+)\[0\]$') {
		$name = $Matches.Name
		if (-not $Target.Contains($name)) { $Target[$name] = @([ordered]@{}) }
		$Target[$name][0][$propertyName] = $Value
		return
	}

	if (-not $Target.Contains($containerName)) { $Target[$containerName] = [ordered]@{} }
	$Target[$containerName][$propertyName] = $Value
}

$baseMappings = @(
	@('EmailInvitationId', 'string'),
	@('TicketNo', 'string'),
	@('FirstName', 'string'),
	@('LastName', 'string'),
	@('SelectPackage', 'string'),
	@('RushNormal', 'string'),
	@('OrderStatus', 'string'),
	@('OrderCreatedAt', 'DateTime'),
	@('OrderCompletedAt', 'DateTime?'),
	@('HitStatus', 'string'),
	@('ReportStatus', 'string'),
	@('ReportUploadedAt', 'DateTime'),
	@('Requestor', 'string'),
	@('ClientId', 'int')) | ForEach-Object {
		[pscustomobject]@{
			Path = $_[0]
			PropertyName = $_[0]
			Type = $_[1]
			EntityName = 'SeedRow'
			IsBase = $true
		}
	}

$relatedEntities = [ordered]@{
	'PersonalDetails' = 'PersonalDetails'
	'AddressDetails' = 'AddressDetails'
	'EducationalBackground' = 'EducationalBackground'
	'LicensesDetails' = 'LicensesDetails'
	'ProfessionalExperiences' = 'ProfessionalExperiences'
	'ReferenceDetails' = 'ReferenceDetails'
	'SignatureDetails' = 'SignatureDetails'
	'Documents[0]' = 'DocumentDetails'
	'ReportDetails[0]' = 'ReportDetails'
	'ArchiveReports[0]' = 'ArchiveReport'
	'OrderStatusHistories[0]' = 'OrderStatusHistory'
	'ApplicantSearchProjection' = 'ApplicantSearchProjection'
}

$mappings = [Collections.Generic.List[object]]::new()
foreach ($mapping in $baseMappings) { $mappings.Add($mapping) }
foreach ($entry in $relatedEntities.GetEnumerator()) {
	foreach ($property in Get-ScalarEntityProperties $entry.Value) {
		$mappings.Add([pscustomobject]@{
			Path = "$($entry.Key).$($property.Name)"
			PropertyName = $property.Name
			Type = $property.Type
			EntityName = $entry.Value
			IsBase = $false
		})
	}
}

$resolvedExcelPath = [IO.Path]::GetFullPath($ExcelPath)
$ownsExcel = $false
$ownsWorkbook = $false
$excel = $null
$workbook = $null
$worksheet = $null
$usedRange = $null
$fullRange = $null

try {
	try {
		$excel = [Runtime.InteropServices.Marshal]::GetActiveObject('Excel.Application')
		foreach ($openWorkbook in $excel.Workbooks) {
			try {
				if ([IO.Path]::GetFullPath([string]$openWorkbook.FullName) -eq $resolvedExcelPath) {
					$workbook = $openWorkbook
					break
				}
			}
			finally {
				if ($null -eq $workbook -or $openWorkbook.FullName -ne $workbook.FullName) {
					[void][Runtime.InteropServices.Marshal]::ReleaseComObject($openWorkbook)
				}
			}
		}
	}
	catch {
		$excel = $null
	}

	if ($null -eq $excel) {
		$excel = New-Object -ComObject Excel.Application
		$excel.Visible = $false
		$excel.DisplayAlerts = $false
		$ownsExcel = $true
	}
	if ($null -eq $workbook) {
		$workbooks = $excel.Workbooks
		try {
			$workbook = $workbooks.Open($resolvedExcelPath, 0, $false)
		}
		finally {
			[void][Runtime.InteropServices.Marshal]::ReleaseComObject($workbooks)
		}
		$ownsWorkbook = $true
	}
	if ($null -eq $workbook -or -not [Runtime.InteropServices.Marshal]::IsComObject($workbook)) {
		throw "Excel did not return a workbook for '$resolvedExcelPath'."
	}

	$worksheet = $workbook.Worksheets.Item(1)
	$usedRange = $worksheet.UsedRange
	$headerColumns = @{}
	$headerNames = @{}
	$lastColumn = $usedRange.Columns.Count
	$rowCount = $usedRange.Rows.Count
	$headersAdded = $false
	for ($column = 1; $column -le $lastColumn; $column++) {
		$header = [string]$worksheet.Cells.Item(1, $column).Value2
		if ([string]::IsNullOrWhiteSpace($header)) { continue }
		$normalized = Get-NormalizedHeader $header
		if ($headerColumns.ContainsKey($normalized)) {
			throw "Duplicate Excel headers '$($headerNames[$normalized])' and '$header' were found."
		}
		$headerColumns[$normalized] = $column
		$headerNames[$normalized] = $header
	}

	foreach ($mapping in $mappings) {
		$normalized = Get-NormalizedHeader $mapping.Path
		if ($headerColumns.ContainsKey($normalized)) { continue }
		$lastColumn++
		$headerCell = $worksheet.Cells.Item(1, $lastColumn)
		$headerCell.NumberFormat = '@'
		$headerCell.Value2 = $mapping.Path
		$headerCell.Font.Bold = $true
		$headerColumns[$normalized] = $lastColumn
		$headerNames[$normalized] = $mapping.Path
		$headersAdded = $true
		[void][Runtime.InteropServices.Marshal]::ReleaseComObject($headerCell)
	}

	# Excel must persist newly extended worksheet columns before accepting a
	# two-dimensional bulk assignment into those ranges.
	if ($headersAdded) { $workbook.Save() }

	# Refresh UsedRange after adding headers so subsequent bulk writes target
	# ranges Excel recognizes as part of the current worksheet shape.
	[void][Runtime.InteropServices.Marshal]::ReleaseComObject($usedRange)
	$usedRange = $worksheet.UsedRange
	$rowCount = [Math]::Max($rowCount, $usedRange.Rows.Count)
	$rangeStart = $worksheet.Cells.Item(1, 1)
	$rangeEnd = $worksheet.Cells.Item($rowCount, $lastColumn)
	try {
		$fullRange = $worksheet.Range($rangeStart, $rangeEnd)
	}
	finally {
		[void][Runtime.InteropServices.Marshal]::ReleaseComObject($rangeStart)
		[void][Runtime.InteropServices.Marshal]::ReleaseComObject($rangeEnd)
	}
	$grid = $fullRange.Value2

	$ticketColumn = $headerColumns[(Get-NormalizedHeader 'TicketNo')]
	$rowNumbers = [Collections.Generic.List[int]]::new()
	for ($rowNumber = 2; $rowNumber -le $rowCount; $rowNumber++) {
		$ticket = [string]$grid[$rowNumber, $ticketColumn]
		if ($ticket -match '^\d{4}-\d+$') { $rowNumbers.Add($rowNumber) }
	}

	$changedColumns = [Collections.Generic.HashSet[int]]::new()
	foreach ($rowNumber in $rowNumbers) {
		$sourceValues = @{}
		foreach ($headerEntry in $headerColumns.GetEnumerator()) {
			$sourceValues[$headerEntry.Key] = $grid[$rowNumber, $headerEntry.Value]
		}
		$ticket = [string]$sourceValues[(Get-NormalizedHeader 'TicketNo')]
		$context = [pscustomobject]@{
			TicketNo = $ticket
			SourceValues = $sourceValues
			CompletedValues = @{}
		}

		foreach ($mapping in $mappings) {
			$column = $headerColumns[(Get-NormalizedHeader $mapping.Path)]
			$value = $grid[$rowNumber, $column]
			if (Test-MissingValue $value) {
				$value = if ($mapping.IsBase) {
					Get-BaseGeneratedValue $context $mapping.PropertyName $mapping.Type
				}
				else {
					Get-RelatedGeneratedValue $context $mapping
				}
				if (Test-MissingValue $value) {
					throw "The generator produced an empty value for '$($mapping.Path)' on Excel row $rowNumber."
				}
				$grid[$rowNumber, $column] = $value
				[void]$changedColumns.Add($column)
			}
			$context.CompletedValues[$mapping.Path] = $grid[$rowNumber, $column]
		}
	}

	foreach ($column in $changedColumns) {
		$columnValues = [object[,]]::new(($rowCount - 1), 1)
		for ($rowNumber = 2; $rowNumber -le $rowCount; $rowNumber++) {
			$columnValues[($rowNumber - 2), 0] = $grid[$rowNumber, $column]
		}
		$columnStart = $worksheet.Cells.Item(2, $column)
		$columnEnd = $worksheet.Cells.Item($rowCount, $column)
		$columnRange = $null
		try {
			$columnRange = $worksheet.Range($columnStart, $columnEnd)
			$mappingForColumn = $mappings | Where-Object {
				$headerColumns[(Get-NormalizedHeader $_.Path)] -eq $column
			} | Select-Object -First 1
			if ($null -ne $mappingForColumn -and
				$mappingForColumn.Type -notmatch '^(int|long|bool)\??$') {
				$columnRange.NumberFormat = '@'
			}
			try {
				$setValueArguments = [object[]]::new(1)
				$setValueArguments[0] = $columnValues
				[void]$columnRange.GetType().InvokeMember(
					'Value2',
					[Reflection.BindingFlags]::SetProperty,
					$null,
					$columnRange,
					$setValueArguments)
			}
			catch {
				$firstValueType = if ($null -eq $columnValues[0, 0]) {
					'<null>'
				}
				else {
					$columnValues[0, 0].GetType().FullName
				}
				throw "Excel could not write mapped column '$($mappingForColumn.Path)' " +
					"($($columnValues.GetLength(0)) rows; first value type: $firstValueType). $($_.Exception.Message)"
			}
		}
		finally {
			if ($null -ne $columnRange) { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($columnRange) }
			[void][Runtime.InteropServices.Marshal]::ReleaseComObject($columnStart)
			[void][Runtime.InteropServices.Marshal]::ReleaseComObject($columnEnd)
		}
	}
	$workbook.Save()

	# Re-read the saved workbook and build JSON only from the completed Excel data.
	$grid = $fullRange.Value2
	$completedRows = [Collections.Generic.List[object]]::new()
	foreach ($rowNumber in $rowNumbers) {
		$jsonRow = [ordered]@{}
		foreach ($mapping in $mappings) {
			$column = $headerColumns[(Get-NormalizedHeader $mapping.Path)]
			$value = Convert-CompletedValueForJson $grid[$rowNumber, $column] $mapping.Type
			Add-CompletedPath $jsonRow $mapping.Path $value
		}
		$completedRows.Add([pscustomobject]$jsonRow)
	}

	$json = $completedRows | ConvertTo-Json -Depth 8
	[IO.File]::WriteAllText(
		[IO.Path]::GetFullPath($JsonPath),
		$json + [Environment]::NewLine,
		[Text.UTF8Encoding]::new($false))

	Write-Output "Completed $($completedRows.Count) Intouch rows."
	Write-Output "Excel saved: $resolvedExcelPath"
	Write-Output "JSON saved: $([IO.Path]::GetFullPath($JsonPath))"
}
finally {
	if ($null -ne $fullRange) { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($fullRange) }
	if ($null -ne $usedRange) { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($usedRange) }
	if ($null -ne $worksheet) { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($worksheet) }
	if ($null -ne $workbook) {
		if ($ownsWorkbook) { $workbook.Close($true) }
		[void][Runtime.InteropServices.Marshal]::ReleaseComObject($workbook)
	}
	if ($null -ne $excel) {
		if ($ownsExcel) { $excel.Quit() }
		[void][Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
	}
	[GC]::Collect()
	[GC]::WaitForPendingFinalizers()
}
