using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Parsing.Tokens;
using VDS.RDF.Writing.Formatting;

namespace Cogs.Publishers;

/// <summary>
/// Formats generator-owned SPARQL by changing whitespace only. An expression
/// parser/formatter can overflow integer constants or rewrite literal values.
/// </summary>
internal static class ShaclQueryFormatter
{
    private const int IndentWidth = 4;
    private const int SoftWidth = 120;
    internal readonly record struct SourceToken(string Text, int Start, int End);

    internal static IReadOnlyList<SourceToken> Tokenize(string query)
    {
        var lines = new List<int> { 0 };
        for (int i = 0; i < query.Length; i++)
        {
            if (query[i] == '\r' && i + 1 < query.Length && query[i + 1] == '\n') i++;
            if (query[i] is '\r' or '\n') lines.Add(i + 1);
        }
        using var input = new StringReader(query);
        var tokenizer = new SparqlTokeniser(input, SparqlQuerySyntax.Sparql_1_1);
        var tokens = new List<SourceToken>();
        while (true)
        {
            IToken token = tokenizer.GetNextToken();
            if (token.TokenType == Token.EOF) break;
            if (token.TokenType == Token.BOF) continue;
            int start = lines[token.StartLine - 1] + token.StartPosition - 1;
            int end = lines[token.EndLine - 1] + token.EndPosition - 1;
            // URI token positions describe the contents, unlike literal token
            // positions which include their delimiters. Keep the original <>.
            if (token.TokenType is Token.URI or Token.DATATYPE && start > 0 && end < query.Length
                && query[start - 1] == '<' && query[end] == '>') { start--; end++; }
            // dotNetRDF 3.5.2 reports incorrect end columns for these two
            // keywords. Recover only their source word, never decoded values.
            if (token is StrContainsKeywordToken or IsLiteralKeywordToken)
            {
                end = start;
                while (end < query.Length && char.IsAsciiLetter(query[end])) end++;
            }
            tokens.Add(new SourceToken(query[start..end], start, end));
        }
        return tokens;
    }

    internal static string Format(string query)
    {
        IReadOnlyList<SourceToken> tokens = Tokenize(query);
        var output = new StringBuilder(query.Length);
        var groupParentheses = new Stack<int>();
        groupParentheses.Push(0);
        int depth = 0, parentheses = 0, column = 0;
        bool lineStart = true, prefix = false;

        void NewLine(bool blank = false)
        {
            while (output.Length > 0 && output[^1] == ' ') output.Length--;
            if (output.Length > 0 && output[^1] != '\n') output.Append('\n');
            if (blank && output.Length > 0 && (output.Length < 2 || output[^2] != '\n')) output.Append('\n');
            lineStart = true;
            column = 0;
        }

        for (int i = 0; i < tokens.Count; i++)
        {
            SourceToken token = tokens[i];
            string text = token.Text;
            string previous = i == 0 ? "" : tokens[i - 1].Text;
            string gap = i == 0 ? query[..token.Start] : query[tokens[i - 1].End..token.Start];
            if (gap.Any(c => !char.IsWhiteSpace(c)))
                throw new InvalidOperationException("Unexpected non-whitespace between generated SPARQL tokens.");

            bool inGroup = parentheses == groupParentheses.Peek();
            if (text.Equals("PREFIX", StringComparison.OrdinalIgnoreCase)) { NewLine(); prefix = true; }
            else if (prefix && previous.StartsWith('<')) { NewLine(blank: true); prefix = false; }
            if (text == "}") { depth--; groupParentheses.Pop(); NewLine(); }
            else if (inGroup && text.ToUpperInvariant() is "SELECT" or "WHERE" or "FILTER" or "BIND"
                or "OPTIONAL" or "UNION" or "GROUP" or "HAVING" or "ORDER" or "LIMIT" or "OFFSET") NewLine();

            bool continuation = previous == "," && column + text.Length + 1 > SoftWidth;
            if (continuation) NewLine();
            if (lineStart)
            {
                int indent = (depth + (continuation ? 1 : 0)) * IndentWidth;
                output.Append(' ', indent);
                column = indent;
                lineStart = false;
            }
            else if (gap.Length > 0 || text == "{") { output.Append(' '); column++; }
            output.Append(text);
            column += text.Length;
            if (text == "(") parentheses++;
            else if (text == ")") parentheses--;
            else if (text == "{") { depth++; groupParentheses.Push(parentheses); NewLine(); }
            else if (text == ".") NewLine();
        }
        return output.ToString().TrimEnd();
    }

    internal static string FormatLiteral(string query, int openingColumn)
    {
        string indent = new(' ', openingColumn + IndentWidth);
        string formatted = Format(query);
        IReadOnlyList<SourceToken> tokens = Tokenize(formatted);
        var value = new StringBuilder("\n");
        int offset = 0, tokenIndex = 0;
        foreach (string line in formatted.Split('\n'))
        {
            while (tokenIndex < tokens.Count && tokens[tokenIndex].End <= offset) tokenIndex++;
            bool insideToken = tokenIndex < tokens.Count && tokens[tokenIndex].Start < offset;
            // A line inside a SPARQL long string is data, not indentation.
            if (line.Length != 0 && !insideToken) value.Append(indent);
            value.Append(line).Append('\n');
            offset += line.Length + 1;
        }
        value.Append(' ', openingColumn);
        using var graph = new Graph();
        // The standard formatter handles Turtle quoting, backslashes, and long
        // literals. Literal CR remains data, not a platform formatting newline.
        return new TurtleW3CFormatter().Format(graph.CreateLiteralNode(value.ToString()))
            .Replace("\r", "\\r", StringComparison.Ordinal);
    }
}
