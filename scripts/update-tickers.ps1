# Regenerates src/LifeTracker.Web/wwwroot/data/tickers.json from public sources.
# Runs on CI before publish and is safe to run locally during development.
#
#  Sources:
#   * NASDAQ Trader symbol directory — every US-listed common / ETF ticker
#       nasdaqlisted.txt  (NASDAQ-listed securities)
#       otherlisted.txt   (NYSE, NYSE American, ARCA, BATS — everything else)
#     These are the authoritative daily-regenerated pipe-delimited files the
#     industry uses. No auth, no User-Agent filtering.
#
#   * CoinGecko /coins/markets — top N crypto by market cap, free public API.
#
# The output is one flat JSON array of { symbol, name, exchange } records,
# sorted, deduplicated by (symbol, exchange). Consumed by StaticTickerCatalog.
#
# Usage (local dev):
#     powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/update-tickers.ps1
# Usage (CI):
#     pwsh scripts/update-tickers.ps1

[CmdletBinding()]
param(
    [int]$CryptoPages = 4,       # 4 x 250 = top 1000 coins
    [int]$CryptoPerPage = 250
)

$ErrorActionPreference = 'Stop'
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$outPath = Join-Path $repoRoot 'src/LifeTracker.Web/wwwroot/data/tickers.json'
$outDir = Split-Path $outPath -Parent
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

function Parse-NasdaqListed($text, $listName) {
    # Format:
    #   Symbol|Security Name|Market Category|Test Issue|...
    #   AAPL|Apple Inc. - Common Stock|Q|N|...
    #   ...
    #   File Creation Time: 1234567890|||...   <- trailer row, skip
    $rows = New-Object 'System.Collections.Generic.List[object]'
    $lines = $text -split "`r?`n"
    if ($lines.Count -lt 2) { return $rows }

    $header = $lines[0] -split '\|'
    $symIdx = [Array]::IndexOf($header, 'Symbol')
    if ($symIdx -lt 0) { $symIdx = [Array]::IndexOf($header, 'ACT Symbol') }
    $nameIdx = [Array]::IndexOf($header, 'Security Name')
    $testIdx = [Array]::IndexOf($header, 'Test Issue')
    $etfIdx  = [Array]::IndexOf($header, 'ETF')
    $exchIdx = [Array]::IndexOf($header, 'Exchange')       # present in otherlisted.txt

    # Noise we never want in the autocomplete: warrants, units, rights,
    # preferred shares, depositary. Match either on the symbol suffix
    # (.W, .WS, +, =, /WS, /U, /R, $P …) or on keywords in the name.
    $skipNameRx = '(?i)\b(Warrant|Unit|Rights?|Depositary|Preferred|Subordinated|Notes?\s+due|Debentures?|Trust Preferred)\b'

    for ($i = 1; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line.StartsWith('File Creation Time')) { continue }

        $cols = $line -split '\|'
        if ($cols.Count -le $symIdx) { continue }

        $sym = $cols[$symIdx]
        if ([string]::IsNullOrWhiteSpace($sym)) { continue }
        $sym = $sym.Trim()

        # Test issues never trade — drop them.
        if ($testIdx -ge 0 -and $cols[$testIdx] -eq 'Y') { continue }

        # Symbols containing . / $ + = or whitespace are derivative-ish
        # listings (warrants, units, preferreds, when-issued). Filter hard.
        if ($sym -match '[\.\$\+\=\s/]') { continue }

        $name = if ($nameIdx -ge 0 -and $cols.Count -gt $nameIdx) { $cols[$nameIdx] } else { '' }
        if ($name -match $skipNameRx) { continue }

        # Clean the trailing boilerplate. NASDAQ emits both
        # "Acme Corp - Common Stock" (with dash) and
        # "Acme Corp. Common Stock" (no dash), so strip either way.
        $name = $name -replace '\s*[-\s]\s*(Class\s+[A-Z]\s+)?(Common Stock|Ordinary Shares?|ADS|American Depositary\s+(Shares?|Receipts?))\s*$', ''
        $name = $name -replace '\s*[-\s]\s*(Beneficial Interest|Capital Stock)\s*$', ''
        $name = $name.Trim()

        $exchange = $listName
        if ($exchIdx -ge 0 -and $cols.Count -gt $exchIdx) {
            switch ($cols[$exchIdx]) {
                'N' { $exchange = 'NYSE' }
                'A' { $exchange = 'NYSE American' }
                'P' { $exchange = 'NYSE Arca' }
                'Z' { $exchange = 'Cboe BZX' }
                'V' { $exchange = 'IEX' }
                default { $exchange = $listName }
            }
        }

        $rows.Add([pscustomobject]@{
            symbol   = $sym
            name     = $name
            exchange = $exchange
        })
    }
    return $rows
}

$us = New-Object 'System.Collections.Generic.List[object]'

Write-Host 'Fetching NASDAQ-listed securities...'
$nasdaqText = Invoke-WebRequest -Uri 'https://www.nasdaqtrader.com/dynamic/SymDir/nasdaqlisted.txt' -TimeoutSec 60 -UseBasicParsing | Select-Object -ExpandProperty Content
$nasdaqRows = Parse-NasdaqListed $nasdaqText 'NASDAQ'
$us.AddRange([object[]]$nasdaqRows)
Write-Host "  $($nasdaqRows.Count) NASDAQ tickers"

Write-Host 'Fetching other US-listed securities...'
$otherText = Invoke-WebRequest -Uri 'https://www.nasdaqtrader.com/dynamic/SymDir/otherlisted.txt' -TimeoutSec 60 -UseBasicParsing | Select-Object -ExpandProperty Content
$otherRows = Parse-NasdaqListed $otherText 'US'
$us.AddRange([object[]]$otherRows)
Write-Host "  $($otherRows.Count) other-listed tickers"

$crypto = New-Object 'System.Collections.Generic.List[object]'
for ($page = 1; $page -le $CryptoPages; $page++) {
    $url = "https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&order=market_cap_desc&per_page=$CryptoPerPage&page=$page&sparkline=false"

    $batch = $null
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        Write-Host "Fetching CoinGecko page $page / $CryptoPages (attempt $attempt)..."
        try {
            $batch = Invoke-RestMethod -Uri $url -TimeoutSec 60
            break
        } catch {
            $status = $null
            if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
            if ($status -eq 429 -and $attempt -lt 4) {
                $wait = [Math]::Pow(2, $attempt) * 15  # 30s, 60s, 120s
                Write-Warning "CoinGecko 429. Backing off $wait s..."
                Start-Sleep -Seconds $wait
                continue
            }
            Write-Warning "CoinGecko page $page failed: $($_.Exception.Message). Stopping crypto fetch."
            $batch = $null
            break
        }
    }
    if (-not $batch) { break }

    foreach ($c in $batch) {
        if (-not $c.symbol) { continue }
        $crypto.Add([pscustomobject]@{
            symbol   = ($c.symbol.ToUpper()) + 'USD'
            name     = [string]$c.name
            exchange = 'Crypto'
        })
    }
    Start-Sleep -Seconds 3  # gentle pacing for the public free tier
}
Write-Host "  $($crypto.Count) crypto tickers"

# Ljubljana Stock Exchange — tiny list the author cares about, hardcoded
# because LJSE has no open API we can hit anonymously. Must be
# [pscustomobject] like the others so Sort-Object treats them uniformly.
$si = @(
    [pscustomobject]@{ symbol = 'KRKG'; name = 'Krka d.d.';                exchange = 'LJSE' }
    [pscustomobject]@{ symbol = 'NLBR'; name = 'Nova Ljubljanska banka';   exchange = 'LJSE' }
    [pscustomobject]@{ symbol = 'PETG'; name = 'Petrol d.d.';              exchange = 'LJSE' }
    [pscustomobject]@{ symbol = 'TLSG'; name = 'Telekom Slovenije';        exchange = 'LJSE' }
    [pscustomobject]@{ symbol = 'ZVTG'; name = 'Zavarovalnica Triglav';    exchange = 'LJSE' }
    [pscustomobject]@{ symbol = 'POSR'; name = 'Pozavarovalnica Sava';     exchange = 'LJSE' }
    [pscustomobject]@{ symbol = 'CICG'; name = 'Cinkarna Celje';           exchange = 'LJSE' }
)

$all = New-Object 'System.Collections.Generic.List[object]'
$all.AddRange([object[]]$us)
$all.AddRange([object[]]$crypto)
$all.AddRange([object[]]$si)

# Dedup by (symbol, exchange) — a crypto and an equity sharing a ticker both survive.
$seen = @{}
$deduped = New-Object 'System.Collections.Generic.List[object]'
foreach ($t in $all) {
    $key = "$($t.symbol)|$($t.exchange)"
    if ($seen.ContainsKey($key)) { continue }
    $seen[$key] = $true
    $deduped.Add($t)
}

$sorted = $deduped | Sort-Object symbol, exchange

Write-Host "Writing $($sorted.Count) entries to $outPath"
# One JSON record per line — valid JSON array, compact enough to keep the
# file small, and git diffs remain line-oriented when tickers are added or
# removed (instead of one giant one-line diff).
$sb = New-Object System.Text.StringBuilder
[void]$sb.Append("[`n")
for ($i = 0; $i -lt $sorted.Count; $i++) {
    $line = $sorted[$i] | ConvertTo-Json -Compress -Depth 3
    [void]$sb.Append('  ').Append($line)
    if ($i -lt $sorted.Count - 1) { [void]$sb.Append(',') }
    [void]$sb.Append("`n")
}
[void]$sb.Append(']')
[System.IO.File]::WriteAllText($outPath, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))

Write-Host 'Done.'
