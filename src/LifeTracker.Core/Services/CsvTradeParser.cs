using System.Globalization;
using LifeTracker.Core.Models;

namespace LifeTracker.Core.Services;

/// <summary>
/// Parses pasted/uploaded CSV into <see cref="Trade"/> rows for the
/// import page. Header-driven so users can reorder columns; missing
/// optional columns degrade gracefully (Notes blank, OpenedAt = today).
///
/// We deliberately keep the parser pure — no DI, no side effects, no
/// repository — so it lives in Core and is straightforward to unit test.
/// Numbers/dates parse with InvariantCulture so a comma decimal in a
/// Slovenian browser locale doesn't suddenly mean "thousands separator."
/// </summary>
public static class CsvTradeParser
{
    /// <summary>
    /// Required + optional column names. Lookups are case-insensitive
    /// and ignore surrounding whitespace.
    /// </summary>
    public static readonly string[] RequiredHeaders =
    {
        "Ticker", "Direction", "AmountInvested", "OpenPrice", "ClosePrice"
    };

    public static readonly string[] OptionalHeaders =
    {
        "OpenedAt", "Notes"
    };

    /// <summary>One row's outcome — either a trade or a row-scoped error.</summary>
    public sealed record RowResult(int LineNumber, Trade? Trade, string? Error)
    {
        public bool IsSuccess => Trade is not null && Error is null;
    }

    public sealed record ParseResult(
        IReadOnlyList<RowResult> Rows,
        IReadOnlyList<string> HeaderErrors)
    {
        public IEnumerable<Trade> Trades => Rows.Where(r => r.IsSuccess).Select(r => r.Trade!);
        public IEnumerable<RowResult> Errors => Rows.Where(r => !r.IsSuccess);
        public bool HasFatalErrors => HeaderErrors.Count > 0;
    }

    public static ParseResult Parse(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return new ParseResult(
                Array.Empty<RowResult>(),
                new[] { "Input is empty." });
        }

        // Normalize line endings before splitting so a Windows-saved file
        // pasted into a textarea on Linux doesn't turn \r\n into a phantom
        // empty row.
        var lines = csv.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        // Skip leading blank lines but track the original line number so
        // error messages point the user at the right row in their editor.
        int idx = 0;
        while (idx < lines.Length && string.IsNullOrWhiteSpace(lines[idx])) idx++;
        if (idx >= lines.Length)
        {
            return new ParseResult(
                Array.Empty<RowResult>(),
                new[] { "Input is empty." });
        }

        var headerCells = SplitCsvLine(lines[idx]);
        var headerMap = BuildHeaderMap(headerCells, out var headerErrors);
        if (headerErrors.Count > 0)
        {
            return new ParseResult(Array.Empty<RowResult>(), headerErrors);
        }

        var rows = new List<RowResult>();
        for (int i = idx + 1; i < lines.Length; i++)
        {
            var raw = lines[i];
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var cells = SplitCsvLine(raw);
            var lineNumber = i + 1; // 1-based for humans
            var rowResult = ParseRow(cells, headerMap, lineNumber);
            rows.Add(rowResult);
        }

        return new ParseResult(rows, Array.Empty<string>());
    }

    private static Dictionary<string, int> BuildHeaderMap(
        string[] headerCells, out List<string> errors)
    {
        errors = new List<string>();
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headerCells.Length; i++)
        {
            var name = headerCells[i].Trim();
            if (name.Length == 0) continue;
            if (!map.TryAdd(name, i))
            {
                errors.Add($"Duplicate header column: {name}");
            }
        }

        foreach (var req in RequiredHeaders)
        {
            if (!map.ContainsKey(req))
            {
                errors.Add($"Missing required column: {req}");
            }
        }

        return map;
    }

    private static RowResult ParseRow(
        string[] cells,
        Dictionary<string, int> headerMap,
        int lineNumber)
    {
        try
        {
            string Get(string col)
            {
                if (!headerMap.TryGetValue(col, out var idx)) return string.Empty;
                return idx < cells.Length ? cells[idx].Trim() : string.Empty;
            }

            var ticker = Get("Ticker").ToUpperInvariant();
            if (ticker.Length == 0)
                return new RowResult(lineNumber, null, "Ticker is empty.");

            var directionRaw = Get("Direction");
            if (!Enum.TryParse<Direction>(directionRaw, ignoreCase: true, out var direction))
                return new RowResult(lineNumber, null,
                    $"Direction '{directionRaw}' must be 'Long' or 'Short'.");

            if (!TryParseDecimal(Get("AmountInvested"), out var amount))
                return new RowResult(lineNumber, null, "AmountInvested is not a number.");

            if (!TryParseDecimal(Get("OpenPrice"), out var openPrice))
                return new RowResult(lineNumber, null, "OpenPrice is not a number.");

            if (!TryParseDecimal(Get("ClosePrice"), out var closePrice))
                return new RowResult(lineNumber, null, "ClosePrice is not a number.");

            // OpenedAt is optional. Empty falls back to today's date so
            // imports without a timestamp still produce useful rows.
            var openedAtRaw = Get("OpenedAt");
            DateTime openedAt;
            if (string.IsNullOrWhiteSpace(openedAtRaw))
            {
                openedAt = DateTime.Today;
            }
            else if (!TryParseDate(openedAtRaw, out openedAt))
            {
                return new RowResult(lineNumber, null,
                    $"OpenedAt '{openedAtRaw}' is not a valid date (YYYY-MM-DD).");
            }

            var trade = new Trade
            {
                Ticker = ticker,
                Direction = direction,
                AmountInvested = amount,
                OpenPrice = openPrice,
                ClosePrice = closePrice,
                OpenedAt = openedAt,
                Notes = Get("Notes"),
            };

            return new RowResult(lineNumber, trade, null);
        }
        catch (Exception ex)
        {
            return new RowResult(lineNumber, null, ex.Message);
        }
    }

    private static bool TryParseDecimal(string s, out decimal value)
    {
        // We accept the dot as decimal separator (broker exports), but
        // also tolerate comma so a Slovenian user pasting from Excel
        // doesn't get bitten. Strip thousands separators (comma OR dot
        // is ambiguous, so we only strip space + non-breaking space).
        s = s.Trim()
             .Replace(" ", "")
             .Replace("\u00A0", "");
        if (s.Length == 0) { value = 0; return false; }

        // If the string contains both '.' and ',', assume the LAST one
        // is the decimal separator and the other is grouping. Otherwise
        // try invariant first (dot decimal), then accept comma decimal.
        if (s.Contains('.') && s.Contains(','))
        {
            var lastDot = s.LastIndexOf('.');
            var lastComma = s.LastIndexOf(',');
            if (lastDot > lastComma)
                s = s.Replace(",", string.Empty); // 1,234.56 -> 1234.56
            else
                s = s.Replace(".", string.Empty).Replace(',', '.'); // 1.234,56 -> 1234.56
        }
        else if (s.Contains(','))
        {
            s = s.Replace(',', '.');
        }

        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseDate(string s, out DateTime value)
    {
        // Accept ISO first (most stable + what our template suggests),
        // then a few common broker / Excel shapes.
        string[] formats =
        {
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy/MM/dd",
            "dd.MM.yyyy",
            "dd/MM/yyyy",
            "MM/dd/yyyy",
        };

        if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out value))
            return true;

        // Last-ditch: invariant general parse (catches e.g. "Jan 15 2026").
        return DateTime.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal, out value);
    }

    /// <summary>
    /// Splits a single CSV line on commas, respecting RFC-4180-style
    /// double-quoted fields (so a Notes value with an embedded comma
    /// stays intact). We don't bring in a CSV library because the only
    /// quirk we need to handle is "double-quoted fields with commas."
    /// </summary>
    private static string[] SplitCsvLine(string line)
    {
        var cells = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Doubled quote = literal quote inside a quoted field.
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == ',')
                {
                    cells.Add(sb.ToString());
                    sb.Clear();
                }
                else if (c == '"' && sb.Length == 0)
                {
                    inQuotes = true;
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        cells.Add(sb.ToString());
        return cells.ToArray();
    }
}
