namespace TxOrganizer.Utilities;

public static class DecimalHelper
{
    /// <summary>
    /// Helper function to count decimal places in a double value
    /// </summary>
    /// <param name="value">The double value to analyze</param>
    /// <returns>Number of decimal places</returns>
    public static int GetDecimalPlaces(double value)
    {
        var str = value.ToString("G5");
        if (!str.Contains('.')) return 0;
        
        // Remove trailing zeros
        str = str.TrimEnd('0');
        if (str.EndsWith('.')) return 0;
        
        return str.Length - str.IndexOf('.') - 1;
    }
}
