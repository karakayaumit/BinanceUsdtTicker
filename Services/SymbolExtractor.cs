using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BinanceUsdtTicker;

/// <summary>
/// Extracts tradable symbols from free form text using common USDT patterns.
/// </summary>
public interface ISymbolExtractor
{
    /// <summary>
    /// Extracts USDT trading pairs from the supplied <paramref name="title"/>.
    /// </summary>
    /// <param name="title">News headline or announcement text.</param>
    /// <returns>List of symbols normalized to the *USDT suffix.</returns>
    IReadOnlyList<string> ExtractUsdtPairs(string title);
}

/// <summary>
/// Default <see cref="ISymbolExtractor"/> implementation based on regular expressions.
/// </summary>
public sealed class RegexSymbolExtractor : ISymbolExtractor
{
    private static readonly Regex UsdtSym = new(@"\b([A-Z0-9]{2,15})(?:/|-)?USDTM?\b", RegexOptions.Compiled);
    private static readonly Regex ParenSym = new(@"\(([A-Z0-9]{2,15})\)", RegexOptions.Compiled);
    private static readonly Regex UpperSym = new(@"\b([A-Z][A-Z0-9]{1,14})\b", RegexOptions.Compiled);

    /// <inheritdoc />
    public IReadOnlyList<string> ExtractUsdtPairs(string title)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in UsdtSym.Matches(title))
            set.Add(m.Groups[1].Value + "USDT");

        foreach (Match m in ParenSym.Matches(title))
        {
            var sym = m.Groups[1].Value;
            if (sym.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
                set.Add(sym);
            else if (sym.EndsWith("USDTM", StringComparison.OrdinalIgnoreCase))
                set.Add(sym[..^1]);
            else
                set.Add(sym + "USDT");
        }

        foreach (Match m in UpperSym.Matches(title))
        {
            var sym = m.Groups[1].Value;
            if (sym.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
                set.Add(sym);
            else if (sym.EndsWith("USDTM", StringComparison.OrdinalIgnoreCase))
                set.Add(sym[..^1]);
            else
                set.Add(sym + "USDT");
        }

        return set.ToList();
    }
}
