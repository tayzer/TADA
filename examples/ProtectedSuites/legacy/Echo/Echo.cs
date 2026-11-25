namespace Legacy;

public static class Echo
{
    public static string Response() => """{"message":"hello"}""";

    public static int Score(int value)
    {
        return value + 3;
    }
}
