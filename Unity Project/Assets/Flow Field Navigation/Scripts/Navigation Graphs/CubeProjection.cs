using UnityEngine;

public static class CubeProjection
{
    #region World <-> Direction

    public static Vector3 WorldToDirection(
        Vector3 center,
        Quaternion rotation,
        Vector3 worldPosition)
    {
        Vector3 dir = (worldPosition - center).normalized;
        return Quaternion.Inverse(rotation) * dir;
    }

    public static Vector3 DirectionToWorld(
        Vector3 center,
        float radius,
        Quaternion rotation,
        Vector3 direction)
    {
        direction = rotation * direction.normalized;
        return center + direction * radius;
    }

    #endregion

    #region Direction <-> Face

    public static CubeFace GetFace(Vector3 direction)
    {
        direction.Normalize();

        float ax = Mathf.Abs(direction.x);
        float ay = Mathf.Abs(direction.y);
        float az = Mathf.Abs(direction.z);

        if (ax >= ay && ax >= az)
            return direction.x >= 0
                ? CubeFace.PositiveX
                : CubeFace.NegativeX;

        if (ay >= ax && ay >= az)
            return direction.y >= 0
                ? CubeFace.PositiveY
                : CubeFace.NegativeY;

        return direction.z >= 0
            ? CubeFace.PositiveZ
            : CubeFace.NegativeZ;
    }

    #endregion

    #region Direction -> Coordinate

    public static CubeCoordinate DirectionToCubeCoordinate(
        Vector3 direction,
        int resolution)
    {
        direction.Normalize();

        CubeFace face = GetFace(direction);

        Vector2 uv = DirectionToUV(face, direction);

        int x = Mathf.Clamp(
            Mathf.FloorToInt(uv.x * resolution),
            0,
            resolution - 1);

        int y = Mathf.Clamp(
            Mathf.FloorToInt(uv.y * resolution),
            0,
            resolution - 1);

        return new CubeCoordinate(face, x, y);
    }

    #endregion

    #region Coordinate -> Direction

    public static Vector3 CubeCoordinateToDirection(
        CubeCoordinate coordinate,
        int resolution)
    {
        float u = (coordinate.X + 0.5f) / resolution;
        float v = (coordinate.Y + 0.5f) / resolution;

        return UVToDirection(
            coordinate.Face,
            u,
            v);
    }

    #endregion

    #region Internals

    public static Vector2 DirectionToUV(
        CubeFace face,
        Vector3 d)
    {
        float u = 0f;
        float v = 0f;

        switch (face)
        {
            case CubeFace.PositiveX:

                u = (-d.z / Mathf.Abs(d.x) + 1f) * 0.5f;
                v = (d.y / Mathf.Abs(d.x) + 1f) * 0.5f;
                break;

            case CubeFace.NegativeX:

                u = (d.z / Mathf.Abs(d.x) + 1f) * 0.5f;
                v = (d.y / Mathf.Abs(d.x) + 1f) * 0.5f;
                break;

            case CubeFace.PositiveY:

                u = (d.x / Mathf.Abs(d.y) + 1f) * 0.5f;
                v = (-d.z / Mathf.Abs(d.y) + 1f) * 0.5f;
                break;

            case CubeFace.NegativeY:

                u = (d.x / Mathf.Abs(d.y) + 1f) * 0.5f;
                v = (d.z / Mathf.Abs(d.y) + 1f) * 0.5f;
                break;

            case CubeFace.PositiveZ:

                u = (d.x / Mathf.Abs(d.z) + 1f) * 0.5f;
                v = (d.y / Mathf.Abs(d.z) + 1f) * 0.5f;
                break;

            case CubeFace.NegativeZ:

                u = (-d.x / Mathf.Abs(d.z) + 1f) * 0.5f;
                v = (d.y / Mathf.Abs(d.z) + 1f) * 0.5f;
                break;
        }

        return new Vector2(u, v);
    }

    public  static Vector3 UVToDirection(
        CubeFace face,
        float u,
        float v)
    {
        u = u * 2f - 1f;
        v = v * 2f - 1f;

        Vector3 p = face switch
        {
            CubeFace.PositiveX => new Vector3(1f, v, -u),
            CubeFace.NegativeX => new Vector3(-1f, v, u),

            CubeFace.PositiveY => new Vector3(u, 1f, -v),
            CubeFace.NegativeY => new Vector3(u, -1f, v),

            CubeFace.PositiveZ => new Vector3(u, v, 1f),
            CubeFace.NegativeZ => new Vector3(-u, v, -1f),

            _ => Vector3.zero
        };

        return p.normalized;
    }

    #endregion
}