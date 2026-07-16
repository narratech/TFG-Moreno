public static class CubeTopology
{
    private static readonly FaceTransition[,] _transitions =
    {
        // =========================
        // Positive X
        // Left   Right  Up    Down
        // =========================
        {
            new FaceTransition(CubeFace.PositiveZ, CubeRotation.None),
            new FaceTransition(CubeFace.NegativeZ, CubeRotation.None),
            new FaceTransition(CubeFace.PositiveY, CubeRotation.Clockwise90),
            new FaceTransition(CubeFace.NegativeY, CubeRotation.Clockwise270)
        },

        // =========================
        // Negative X
        // =========================
        {
            new FaceTransition(CubeFace.NegativeZ, CubeRotation.None),
            new FaceTransition(CubeFace.PositiveZ, CubeRotation.None),
            new FaceTransition(CubeFace.PositiveY, CubeRotation.Clockwise270),
            new FaceTransition(CubeFace.NegativeY, CubeRotation.Clockwise90)
        },

        // =========================
        // Positive Y
        // =========================
        {
            new FaceTransition(CubeFace.NegativeX, CubeRotation.Clockwise90),
            new FaceTransition(CubeFace.PositiveX, CubeRotation.Clockwise270),
            new FaceTransition(CubeFace.NegativeZ, CubeRotation.Clockwise180),
            new FaceTransition(CubeFace.PositiveZ, CubeRotation.None)
        },

        // =========================
        // Negative Y
        // =========================
        {
            new FaceTransition(CubeFace.NegativeX, CubeRotation.Clockwise270),
            new FaceTransition(CubeFace.PositiveX, CubeRotation.Clockwise90),
            new FaceTransition(CubeFace.PositiveZ, CubeRotation.None),
            new FaceTransition(CubeFace.NegativeZ, CubeRotation.Clockwise180)
        },

        // =========================
        // Positive Z
        // =========================
        {
            new FaceTransition(CubeFace.NegativeX, CubeRotation.None),
            new FaceTransition(CubeFace.PositiveX, CubeRotation.None),
            new FaceTransition(CubeFace.PositiveY, CubeRotation.None),
            new FaceTransition(CubeFace.NegativeY, CubeRotation.None)
        },

        // =========================
        // Negative Z
        // =========================
        {
            new FaceTransition(CubeFace.PositiveX, CubeRotation.None),
            new FaceTransition(CubeFace.NegativeX, CubeRotation.None),
            new FaceTransition(CubeFace.PositiveY, CubeRotation.Clockwise180),
            new FaceTransition(CubeFace.NegativeY, CubeRotation.Clockwise180)
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

        // Coordenadas sobre el borde
        switch (direction)
        {
            case CubeDirection.Left:
                x = resolution - 1;
                break;

            case CubeDirection.Right:
                x = 0;
                break;

            case CubeDirection.Up:
                y = 0;
                break;

            case CubeDirection.Down:
                y = resolution - 1;
                break;
        }

        RotateCoordinate(
            ref x,
            ref y,
            resolution,
            transition.Rotation);

        return new CubeCoordinate(
            transition.Face,
            x,
            y);
    }

    private static void RotateCoordinate(
        ref int x,
        ref int y,
        int resolution,
        CubeRotation rotation)
    {
        int max = resolution - 1;

        switch (rotation)
        {
            case CubeRotation.None:
                return;

            case CubeRotation.Clockwise90:
                {
                    int oldX = x;
                    x = y;
                    y = max - oldX;
                    break;
                }

            case CubeRotation.Clockwise180:
                {
                    x = max - x;
                    y = max - y;
                    break;
                }

            case CubeRotation.Clockwise270:
                {
                    int oldX = x;
                    x = max - y;
                    y = oldX;
                    break;
                }
        }
    }
}