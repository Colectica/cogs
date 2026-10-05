using System.Text.RegularExpressions;

namespace Cogs.Common
{
    /// <summary>RFC 3986 Appendix A grammar, without resolution or normalization.</summary>
    public static class CogsUriReference
    {
        public static readonly string Pattern = BuildPattern();
        private static readonly Regex Grammar = new Regex("\\A(?:" + Pattern + ")\\z", RegexOptions.CultureInvariant);

        public static bool IsValid(string value)
        {
            return value is not null && Grammar.IsMatch(value);
        }

        private static string BuildPattern()
        {
            const string unreserved = @"A-Za-z0-9._~";
            const string subdelimiters = @"!$&'()*+,;=";
            const string encoded = @"%[0-9A-Fa-f]{2}";
            const string h16 = @"[0-9A-Fa-f]{1,4}";
            const string octet = @"(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])";
            string ipv4 = octet + @"(?:\." + octet + "){3}";
            string ls32 = "(?:" + h16 + ":" + h16 + "|" + ipv4 + ")";
            string ipv6 = "(?:" + h16 + ":){6}" + ls32 + "|::(?:" + h16 + ":){5}" + ls32;
            for (int leading = 0; leading <= 6; leading++)
            {
                string prefix = leading == 0 ? h16 + "?" : "(?:" + h16 + ":){0," + leading + "}" + h16;
                // Zero or more leading groups, then the compression marker.
                prefix = leading == 0 ? "(?:" + h16 + ")?" : "(?:" + prefix + ")?";
                int remaining = 4 - leading;
                string suffix = remaining >= 0 ? "(?:" + h16 + ":){" + remaining + "}" + ls32
                    : remaining == -1 ? h16 : string.Empty;
                ipv6 += "|" + prefix + "::" + suffix;
            }
            string ipLiteral = @"\[(?:" + ipv6 + "|[vV][0-9A-Fa-f]+\\.[" + unreserved + subdelimiters + @":-]+)\]";
            string regName = "(?:[" + unreserved + subdelimiters + "-]|" + encoded + ")*";
            string userInfo = "(?:[" + unreserved + subdelimiters + ":-]|" + encoded + ")*";
            string authority = "(?:" + userInfo + "@)?(?:" + ipLiteral + "|" + regName + ")(?::[0-9]*)?";
            string pchar = "(?:[" + unreserved + subdelimiters + ":@-]|" + encoded + ")";
            string segment = pchar + "*";
            string absolute = "/(?:" + pchar + "+(?:/" + segment + ")*)?";
            string path = "(?://" + authority + "(?:/" + segment + ")*|" + absolute + "|" + pchar + "+(?:/" + segment + ")*|)";
            string noScheme = "(?:[" + unreserved + subdelimiters + "@-]|" + encoded + ")+";
            string relative = "(?://" + authority + "(?:/" + segment + ")*|" + absolute + "|" + noScheme + "(?:/" + segment + ")*|)";
            string query = "(?:" + pchar + "|[/?])*";
            return "(?:[A-Za-z][A-Za-z0-9+.-]*:" + path + "|" + relative + ")(?:\\?" + query + ")?(?:#" + query + ")?";
        }
    }
}
