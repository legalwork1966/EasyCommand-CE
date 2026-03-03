using System;
using System.Text.RegularExpressions;

namespace EasyCommand.Services;

public static class CommandSafety
{
    // Broad, conservative detection. Purpose: prompt for confirmation.
    // False positives are acceptable; false negatives are the risk.

    private static readonly Regex PowerShellDanger = new(
        @"\b(remove-item|del|erase|rd|rmdir|format-volume|clear-disk|initialize-disk|diskpart|reg\s+(add|delete)|bcdedit|shutdown|restart-computer|stop-computer|disable-netadapter|netsh\s+advfirewall\s+set\s+allprofiles\s+state\s+off|vssadmin\s+delete|cipher\s+/w)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CmdDanger = new(
        @"\b(del|erase|rd|rmdir|format(\.com)?|diskpart|reg\s+(add|delete)|bcdedit|shutdown|netsh\s+advfirewall\s+set\s+allprofiles\s+state\s+off|vssadmin\s+delete|cipher\s+/w)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool LooksPotentiallyDestructive(string command, bool isPowerShell)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        string trimmed = command.Trim();

        // Multi-command separators raise risk.
        if (trimmed.Contains("&&") || trimmed.Contains("||") || trimmed.Contains(";") || trimmed.Contains("|"))
        {
            return true;
        }

        return isPowerShell
            ? PowerShellDanger.IsMatch(trimmed)
            : CmdDanger.IsMatch(trimmed);
    }

    public static string GetConfirmationText()
    {
        return "This command looks potentially destructive or system-altering.\n\n" +
               "Review it carefully before proceeding.\n\n" +
               "Run anyway?";
    }
}
