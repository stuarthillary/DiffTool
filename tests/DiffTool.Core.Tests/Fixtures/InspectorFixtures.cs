#pragma warning disable CA1822, CS0649, IDE0051, IDE0044
namespace DiffTool.Core.Tests.Fixtures;

// Types with a deliberately varied public surface. AssemblyInspectorTests reads this test
// assembly with the real inspector and asserts on how each shape is reported.

public interface IFixtureShape
{
    string Name { get; }
    void Run();
}

public enum FixtureColor
{
    Red = 1,
    Green = 2,
}

public delegate int FixtureHandler(string text, int count);

public struct FixturePoint
{
    public int X;
    public int Y;
}

public static class FixtureStatics
{
    public const int Answer = 42;
    public static readonly string Label = "label";

    public static int Twice(int value) => value * 2;
}

public abstract class FixtureBase
{
    public abstract void Do();

    public virtual void Maybe() { }
}

public class FixtureDerived : FixtureBase, IFixtureShape
{
    public static int Counter;

    public string Name { get; set; } = "";
    public string ReadOnlyProp { get; } = "";
    public string PrivateSetter { get; private set; } = "";

    public override void Do() { }

    public override void Maybe() { }

    public void Run() { }

    public void Over(int value) { }

    public void Over(string value) { }

    protected void ProtectedMethod() { }

    internal void InternalMethod() { }

    public class Inner
    {
        public int Value;
    }

    internal class HiddenInner
    {
        public int Value;
    }
}

public sealed class FixtureSealed
{
    public int Value;
}

public class FixtureGeneric<T>
{
    public T? Value;
}

internal class FixtureInternal
{
    public int Hidden;

    public class NestedInInternal
    {
        public int Value;
    }
}
