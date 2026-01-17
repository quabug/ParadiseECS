namespace Paradise.ECS.Test;

/// <summary>
/// Test components for unit testing with explicit manual IDs.
/// </summary>
[Component("5B9313BE-CB77-4C8B-A0E4-82A3B369C717", Id = 0)]
public partial struct TestHealth
{
    public int Current;
    public int Max;
}

[Component("B6170E3B-FEE1-4C16-85C9-B5130A253BAC", Id = 1)]
public partial struct TestPosition
{
    public float X, Y, Z;
}

[Component("1040E96A-7D4A-4241-BDE1-36D4DBFCF7C0", Id = 2)]
public partial struct TestVelocity
{
    public float X, Y, Z;
}

[Component("A7B3C4D5-E6F7-4890-ABCD-1234567890AB", Id = 3)]
public partial struct TestTag;

[Component("B8C4D5E6-F7A8-4901-BCDE-2345678901BC", Id = 4)]
public partial struct TestDamage
{
    public int Amount;
}
