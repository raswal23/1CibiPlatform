<#
.SYNOPSIS
    Converts the Jobstreet contact workbook into the seed script for the
    EmploymentVerificationContacts migration.

.DESCRIPTION
    A one-off generator, not production code: it exists so the committed .sql is a
    reviewable artifact rather than a 1.2 MB string literal compiled into every build,
    and so a refreshed workbook can be re-run to produce only the genuinely new rows.

    Ids are UUIDv5 over lower(trim(email)) with a fixed namespace. That makes the
    output deterministic across machines and environments, keeps integration tests
    stable, and means an unchanged email keeps its id when the workbook is reissued -
    so a follow-up migration's ON CONFLICT DO NOTHING skips it.

    Validation is fail-fast rather than best-effort. A silently truncated company name
    or a stray '|' would corrupt the keyset cursor (see ContactRules), and finding that
    out in production is much more expensive than failing here.

.EXAMPLE
    ./Tools/generate-ev-contacts-seed.ps1 `
        -WorkbookPath "$env:USERPROFILE\Desktop\Contact Database for Jobstreet.xlsx" `
        -OutputPath   "BackendAPI/API/APIs/Migrations/EmploymentVerification/EmploymentVerificationContactsSeed.sql"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WorkbookPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    # 500 keeps any one statement small enough to open in a SQL tool and to name the
    # offending batch on failure. The whole migration still runs in one EF transaction,
    # so batching changes nothing about atomicity.
    [int]$BatchSize = 500
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 does not load these by default.
Add-Type -AssemblyName System.Xml.Linq
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Fixed namespace GUID for EV contact ids. Never change it: every id in the committed
# seed derives from it, and a new value would re-key all 7,358 rows.
$NamespaceGuid = [Guid]'6f9b1f2c-7d4e-4a8b-9c3d-2e5a71b04f88'

function New-UuidV5 {
    param([Guid]$Namespace, [string]$Name)

    # RFC 4122 §4.3: SHA-1 over the big-endian namespace followed by the name, then
    # overwrite the version and variant bits.
    $nsBytes = $Namespace.ToByteArray()
    foreach ($span in @(@(0, 3), @(4, 5), @(6, 7))) {
        [Array]::Reverse($nsBytes, $span[0], $span[1] - $span[0] + 1)
    }

    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try {
        $nameBytes = [System.Text.Encoding]::UTF8.GetBytes($Name)
        $buffer = New-Object byte[] ($nsBytes.Length + $nameBytes.Length)
        [Array]::Copy($nsBytes, 0, $buffer, 0, $nsBytes.Length)
        [Array]::Copy($nameBytes, 0, $buffer, $nsBytes.Length, $nameBytes.Length)
        $hash = $sha1.ComputeHash($buffer)
    }
    finally {
        $sha1.Dispose()
    }

    $guidBytes = New-Object byte[] 16
    [Array]::Copy($hash, 0, $guidBytes, 0, 16)
    $guidBytes[6] = ($guidBytes[6] -band 0x0F) -bor 0x50   # version 5
    $guidBytes[8] = ($guidBytes[8] -band 0x3F) -bor 0x80   # RFC 4122 variant

    foreach ($span in @(@(0, 3), @(4, 5), @(6, 7))) {
        [Array]::Reverse($guidBytes, $span[0], $span[1] - $span[0] + 1)
    }

    return [Guid]::new($guidBytes)
}

function ConvertTo-CleanCell {
    param([string]$Value)

    if ($null -eq $Value) { return '' }

    # NBSP first: Excel exports are full of them and they survive Trim(), which would
    # then let two visually identical companies sort apart and defeat the unique index.
    $clean = $Value -replace "`u{00A0}", ' '
    $clean = $clean -replace '\s+', ' '
    $clean = $clean.Trim()

    # One row in the source workbook ends "PRIMESOFT PHILIPPINES, INC (PPI) |" - a
    # trailing separator left behind by whoever compiled the list, not part of the
    # name. Stripped here because it is unambiguously noise at the edge; an interior
    # '|' is left alone and fails the check below, since there the intent is genuinely
    # unclear and a human should decide.
    return $clean.Trim(@(' ', '|')).Trim()
}

function ConvertTo-SqlLiteral {
    param([string]$Value)

    # Doubling the quote is the whole escape: standard_conforming_strings is on in
    # PostgreSQL 16, so a backslash is a literal backslash and E'...' is not needed.
    return "'" + ($Value -replace "'", "''") + "'"
}

Write-Host "Reading $WorkbookPath"

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("ev_seed_" + [Guid]::NewGuid().ToString('N'))
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($WorkbookPath, $tempDir)

try {
    $sharedDoc = [System.Xml.Linq.XDocument]::Load((Join-Path $tempDir 'xl\sharedStrings.xml'))
    $sharedNs = $sharedDoc.Root.Name.Namespace
    $shared = New-Object System.Collections.Generic.List[string]

    foreach ($si in $sharedDoc.Root.Elements($sharedNs + 'si')) {
        $shared.Add((($si.Descendants($sharedNs + 't') | ForEach-Object { $_.Value }) -join ''))
    }

    $sheetDoc = [System.Xml.Linq.XDocument]::Load((Join-Path $tempDir 'xl\worksheets\sheet1.xml'))
    $sheetNs = $sheetDoc.Root.Name.Namespace

    $rows = New-Object System.Collections.Generic.List[object]

    foreach ($row in $sheetDoc.Root.Element($sheetNs + 'sheetData').Elements($sheetNs + 'row')) {
        $rowNumber = [int]$row.Attribute('r').Value
        if ($rowNumber -eq 1) { continue }   # header

        $company = ''
        $email = ''

        foreach ($cell in $row.Elements($sheetNs + 'c')) {
            $column = $cell.Attribute('r').Value -replace '\d', ''
            $type = if ($cell.Attribute('t')) { $cell.Attribute('t').Value } else { '' }
            $valueElement = $cell.Element($sheetNs + 'v')
            $value = if ($valueElement) { $valueElement.Value } else { '' }

            if ($type -eq 's' -and $value -ne '') {
                $value = $shared[[int]$value]
            }
            elseif ($type -eq 'inlineStr') {
                $value = ($cell.Descendants($sheetNs + 't') | ForEach-Object { $_.Value }) -join ''
            }

            if ($column -eq 'A') { $company = $value }
            elseif ($column -eq 'B') { $email = $value }
        }

        $company = ConvertTo-CleanCell $company
        $email = (ConvertTo-CleanCell $email).ToLowerInvariant()

        if ([string]::IsNullOrWhiteSpace($company) -and [string]::IsNullOrWhiteSpace($email)) {
            continue
        }

        if ([string]::IsNullOrWhiteSpace($company)) { throw "Row $rowNumber has no company name." }
        if ([string]::IsNullOrWhiteSpace($email)) { throw "Row $rowNumber has no email address." }
        if ($company.Length -gt 150) { throw "Row $rowNumber company name exceeds 150 characters: $company" }
        if ($email.Length -gt 320) { throw "Row $rowNumber email exceeds 320 characters: $email" }
        if ($company.Contains('|')) { throw "Row $rowNumber company name contains '|', which breaks the keyset cursor: $company" }
        if ($email -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') { throw "Row $rowNumber email is not a valid address: $email" }
        if ($company -match '[\p{Cc}]' -or $email -match '[\p{Cc}]') { throw "Row $rowNumber contains a control character." }

        $rows.Add([pscustomobject]@{
            Id      = (New-UuidV5 -Namespace $NamespaceGuid -Name $email).ToString()
            Company = $company
            Email   = $email
        })
    }
}
finally {
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

# Collapse pairs that differ only by the whitespace/case normalisation above: the
# unique index would reject them at insert time, and DO NOTHING would mask it.
$deduped = $rows | Group-Object { "$($_.Company.ToLowerInvariant())|$($_.Email)" } |
    ForEach-Object { $_.Group[0] }

$skipped = $rows.Count - $deduped.Count
Write-Host "Parsed $($rows.Count) rows; $($deduped.Count) unique (company, email) pairs; $skipped collapsed."

$builder = New-Object System.Text.StringBuilder
[void]$builder.AppendLine('-- Generated by Tools/generate-ev-contacts-seed.ps1. Do not hand-edit.')
[void]$builder.AppendLine('-- Source: "Contact Database for Jobstreet.xlsx", sheet "Contact Database".')
[void]$builder.AppendLine('--')
[void]$builder.AppendLine('-- Ids are UUIDv5 over lower(trim(email)), so re-running the generator against a')
[void]$builder.AppendLine('-- refreshed workbook yields the same id for every unchanged mailbox and the')
[void]$builder.AppendLine('-- ON CONFLICT DO NOTHING below skips it.')
[void]$builder.AppendLine('--')
[void]$builder.AppendLine('-- DO NOTHING rather than DO UPDATE: these rows are editable through the console,')
[void]$builder.AppendLine('-- so a re-run must never overwrite an operator''s correction.')
[void]$builder.AppendLine()

$index = 0
while ($index -lt $deduped.Count) {
    $batch = $deduped[$index..([Math]::Min($index + $BatchSize - 1, $deduped.Count - 1))]

    [void]$builder.AppendLine('INSERT INTO employment_verification."EmploymentVerificationContacts"')
    [void]$builder.AppendLine('    ("Id", "CompanyName", "EmailAddress", "IsActive", "CreatedAt", "UpdatedAt")')
    [void]$builder.AppendLine('VALUES')

    for ($offset = 0; $offset -lt $batch.Count; $offset++) {
        $row = $batch[$offset]
        $terminator = if ($offset -eq $batch.Count - 1) { '' } else { ',' }

        [void]$builder.AppendLine(
            "    ('$($row.Id)'::uuid, $(ConvertTo-SqlLiteral $row.Company), $(ConvertTo-SqlLiteral $row.Email), TRUE, NOW(), NOW())$terminator")
    }

    [void]$builder.AppendLine('ON CONFLICT ("Id") DO NOTHING;')
    [void]$builder.AppendLine()

    $index += $BatchSize
}

$resolvedOutput = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputPath))
[System.IO.File]::WriteAllText($resolvedOutput, $builder.ToString(), (New-Object System.Text.UTF8Encoding $false))

Write-Host "Wrote $($deduped.Count) rows to $resolvedOutput"
