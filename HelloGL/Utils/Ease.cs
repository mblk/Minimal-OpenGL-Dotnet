public static class Ease
{
    public static float InOutExpo(float input)
    {
        if (input <= 0.0f)
            return 0.0f;

        if (input >= 1.0f)
            return 1.0f;

        if (input < 0.5f)
            return MathF.Pow(2.0f, 20.0f * input - 10.0f) / 2.0f;

        return (2.0f - MathF.Pow(2.0f, -20.0f * input + 10.0f)) / 2.0f;
    }
}