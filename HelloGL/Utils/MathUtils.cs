namespace HelloGL.Utils;

public static class MathUtils
{
    //extension(float t)
    //{
    //    public float Lerp(float a, float b)
    //    {
    //        return a + t * (b - a);
    //    }
    //}

    public static float Lerp(float a, float b, float t)
    {
        return a + t * (b - a);
    }

    public static float InverseLerp(float a, float b, float value)
    {
        return (value - a) / (b - a);
    }

    public static float Remap(float inA, float inB, float outA, float outB, float value)
    {
        return outA + (value - inA) / (inB - inA) * (outB - outA);
    }
}
