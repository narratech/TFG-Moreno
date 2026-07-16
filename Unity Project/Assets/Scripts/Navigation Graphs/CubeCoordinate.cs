using UnityEngine;
public enum CubeFace
{
    PositiveX,
    NegativeX,

    PositiveY,
    NegativeY,

    PositiveZ,
    NegativeZ
}
public enum CubeDirection
{
    Left,
    Right,
    Up,
    Down
}

public readonly struct CubeCoordinate
{
    public readonly CubeFace Face;

    public readonly int X;
    public readonly int Y;

    public CubeCoordinate(
        CubeFace face,
        int x,
        int y)
    {
        Face = face;
        X = x;
        Y = y;
    }

    public override string ToString()
    {
        return $"{Face} ({X}, {Y})";
    }
}

public enum CubeRotation
{
    None = 0,

    Clockwise90 = 1,

    Clockwise180 = 2,

    Clockwise270 = 3
}