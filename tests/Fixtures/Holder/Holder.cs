using Missing;

namespace Holder;

public class HolderPlugin
{
    public MissingType Other => null;
}

public static class HolderStatics
{
    public static MissingType Field;
}

public static class Factory
{
    public static object Create() => new HolderPlugin();
}
