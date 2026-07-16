public enum CubeEdge
{
    Left,
    Right,
    Up,
    Down
}

public readonly struct FaceTransition
{
    public readonly CubeFace Face;
    public readonly CubeEdge EnterEdge;
    public readonly bool Flip;

    public FaceTransition(
        CubeFace face,
        CubeEdge enterEdge,
        bool flip = false)
    {
        Face = face;
        EnterEdge = enterEdge;
        Flip = flip;
    }
}

public static class CubeTopology
{
    private static readonly FaceTransition[,] _transitions =
    {
        // ======================================================
        // PositiveX
        // Left            Right             Up               Down
        // ======================================================
        {
            new FaceTransition(CubeFace.PositiveZ, CubeEdge.Right),
            new FaceTransition(CubeFace.NegativeZ, CubeEdge.Left),
            new FaceTransition(CubeFace.PositiveY, CubeEdge.Right),
            new FaceTransition(CubeFace.NegativeY, CubeEdge.Right, true)
        },

        // ======================================================
        // NegativeX
        // ======================================================
        {
            new FaceTransition(CubeFace.NegativeZ, CubeEdge.Right),
            new FaceTransition(CubeFace.PositiveZ, CubeEdge.Left),
            new FaceTransition(CubeFace.PositiveY, CubeEdge.Left, true),
            new FaceTransition(CubeFace.NegativeY, CubeEdge.Left)
        },

        // ======================================================
        // PositiveY
        // ======================================================
        {
            new FaceTransition(CubeFace.NegativeX, CubeEdge.Up),
            new FaceTransition(CubeFace.PositiveX, CubeEdge.Up),
            new FaceTransition(CubeFace.NegativeZ, CubeEdge.Up),
            new FaceTransition(CubeFace.PositiveZ, CubeEdge.Up)
        },

        // ======================================================
        // NegativeY
        // ======================================================
        {
            new FaceTransition(CubeFace.NegativeX, CubeEdge.Down),
            new FaceTransition(CubeFace.PositiveX, CubeEdge.Down),
            new FaceTransition(CubeFace.PositiveZ, CubeEdge.Down),
            new FaceTransition(CubeFace.NegativeZ, CubeEdge.Down)
        },

        // ======================================================
        // PositiveZ
        // ======================================================
        {
            new FaceTransition(CubeFace.NegativeX, CubeEdge.Right),
            new FaceTransition(CubeFace.PositiveX, CubeEdge.Left),
            new FaceTransition(CubeFace.PositiveY, CubeEdge.Down),
            new FaceTransition(CubeFace.NegativeY, CubeEdge.Up)
        },

        // ======================================================
        // NegativeZ
        // ======================================================
        {
            new FaceTransition(CubeFace.PositiveX, CubeEdge.Right),
            new FaceTransition(CubeFace.NegativeX, CubeEdge.Left),
            new FaceTransition(CubeFace.PositiveY, CubeEdge.Up),
            new FaceTransition(CubeFace.NegativeY, CubeEdge.Down)
        }
    };

    public static CubeCoordinate GetNeighbor(
    CubeCoordinate coord,
    CubeDirection direction,
    int resolution)
    {
        int x = coord.X;
        int y = coord.Y;

        switch (direction)
        {
            case CubeDirection.Left: x--; break;
            case CubeDirection.Right: x++; break;
            case CubeDirection.Up: y++; break;
            case CubeDirection.Down: y--; break;
        }

        // Sigue dentro de la cara
        if (x >= 0 &&
            x < resolution &&
            y >= 0 &&
            y < resolution)
        {
            return new CubeCoordinate(coord.Face, x, y);
        }

        FaceTransition transition =
            _transitions[(int)coord.Face, (int)direction];

        // Parámetro sobre la arista
        int t = direction switch
        {
            CubeDirection.Left => coord.Y,
            CubeDirection.Right => coord.Y,
            CubeDirection.Up => coord.X,
            CubeDirection.Down => coord.X,
            _ => 0
        };

        if (transition.Flip)
            t = resolution - 1 - t;

        int nx = 0;
        int ny = 0;

        switch (transition.EnterEdge)
        {
            case CubeEdge.Left:
                nx = 0;
                ny = t;
                break;

            case CubeEdge.Right:
                nx = resolution - 1;
                ny = t;
                break;

            case CubeEdge.Up:
                nx = t;
                ny = resolution - 1;
                break;

            case CubeEdge.Down:
                nx = t;
                ny = 0;
                break;
        }

        return new CubeCoordinate(
            transition.Face,
            nx,
            ny);
    }
}