using System.Diagnostics;
using System.Numerics;

namespace HelloGL.Utils;

public static class EnumExtensions
{
    public static T Next<T>(this T current, int delta)
        where T : struct, Enum
    {
        var allValues = Enum.GetValues<T>();
        var currentIndex = allValues.IndexOf(current);
        Debug.Assert(currentIndex != -1);
        var newIndex = currentIndex + delta;

        // check bounds
        while (newIndex < 0) newIndex += allValues.Length;
        newIndex %= allValues.Length;

        var newValue = allValues[newIndex];
        return newValue;
    }
}
