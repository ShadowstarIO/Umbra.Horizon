using System;
using System.Numerics;

namespace Umbra.Horizon;

internal static class Heading
{
    public const float Deg2Rad = MathF.PI / 180f;
    public const float Rad2Deg = 180f / MathF.PI;
    public const float TwoPi   = MathF.PI * 2f;

    /// <summary>
    /// Shortest signed angle from <paramref name="from"/> to <paramref name="to"/> in radians, range (-π, π].
    /// </summary>
    public static float SignedAngle(Vector2 from, Vector2 to)
    {
        var a = MathF.Atan2(from.Y, from.X);
        var b = MathF.Atan2(to.Y, to.X);
        return WrapPi(b - a);
    }

    public static float WrapPi(float radians)
    {
        while (radians > MathF.PI) radians  -= TwoPi;
        while (radians <= -MathF.PI) radians += TwoPi;
        return radians;
    }

    /// <summary>
    /// Linear heading projection. FOV is the visible half-span in radians.
    /// x = 0 is center. Values outside ±1 are off-strip.
    /// </summary>
    public static float ProjectLinear(float signedAngle, float halfFov)
    {
        if (halfFov <= 0.01f) return signedAngle > 0 ? 2f : -2f;
        return signedAngle / halfFov;
    }

    /// <summary>
    /// Camera forward in a Y-up 2D plane. 0 yaw faces north (+Y).
    /// </summary>
    public static Vector2 ForwardFromYaw(float yawRadians)
    {
        return new Vector2(-MathF.Sin(yawRadians), MathF.Cos(yawRadians));
    }

    public static Vector2 DirectionTo(Vector2 from, Vector2 to)
    {
        var d = to - from;
        var len = d.Length();
        return len < 0.0001f ? Vector2.UnitY : d / len;
    }
}
