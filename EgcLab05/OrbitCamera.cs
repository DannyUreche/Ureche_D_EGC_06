using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Camera orbita (tema): priveste mereu spre o tinta fixa si se misca pe o sfera in jurul ei,
/// data de distanta (raza) si doua unghiuri: azimutul (in jurul axei Y, ca yaw) si elevatia
/// (deasupra planului orizontal, ca pitch).
/// </summary>
public class OrbitCamera : Camera
{
    public Vector3 Target = Vector3.Zero;

    public float Distance { get; private set; } = 6.0f;
    public float Azimuth { get; private set; } = -90.0f;
    public float Elevation { get; private set; } = 20.0f;

    public const float MaxElevation = 89.0f;
    public float MouseSensitivity = 0.3f;        // grade pe pixel
    public float KeyboardRotateSpeed = 60.0f;    // grade pe secunda
    public float DistanceStep = 0.5f;            // unitati pentru un pas al rotitei
    public const float MinDistance = 2.0f;
    public const float MaxDistance = 20.0f;

    private static readonly Vector3 StartTarget = Vector3.Zero;
    private const float StartDistance = 6.0f;
    private const float StartAzimuth = -90.0f;
    private const float StartElevation = 20.0f;

    public override string Name => "orbită";

    public override Vector3 Position
    {
        get
        {
            float azRad = MathHelper.DegreesToRadians(Azimuth);
            float elRad = MathHelper.DegreesToRadians(Elevation);
            Vector3 offset = new(
                MathF.Cos(elRad) * MathF.Cos(azRad),
                MathF.Sin(elRad),
                MathF.Cos(elRad) * MathF.Sin(azRad));
            return Target + Distance * offset;
        }
    }

    public override Vector3 Front => Vector3.Normalize(Target - Position);

    public override Matrix4 ViewMatrix => Matrix4.LookAt(Position, Target, Vector3.UnitY);

    public override void Update(KeyboardState keyboard, MouseState mouse, Vector2 mouseDelta, float dt)
    {
        // Clic stanga tinut + miscarea mouse-ului roteste camera
        if (mouse.IsButtonDown(MouseButton.Left))
        {
            Azimuth += mouseDelta.X * MouseSensitivity;
            Elevation -= mouseDelta.Y * MouseSensitivity;
        }

        // Sagetile rotesc camera de la tastatura
        if (keyboard.IsKeyDown(Keys.Left)) Azimuth -= KeyboardRotateSpeed * dt;
        if (keyboard.IsKeyDown(Keys.Right)) Azimuth += KeyboardRotateSpeed * dt;
        if (keyboard.IsKeyDown(Keys.Up)) Elevation += KeyboardRotateSpeed * dt;
        if (keyboard.IsKeyDown(Keys.Down)) Elevation -= KeyboardRotateSpeed * dt;

        Elevation = Math.Clamp(Elevation, -MaxElevation, MaxElevation);

        // Rotita schimba distanta, nu FOV-ul
        Distance -= mouse.ScrollDelta.Y * DistanceStep;
        Distance = Math.Clamp(Distance, MinDistance, MaxDistance);
    }

    public override void Reset()
    {
        Target = StartTarget;
        Distance = StartDistance;
        Azimuth = StartAzimuth;
        Elevation = StartElevation;
        FieldOfView = 60.0f;
    }

    public override IEnumerable<string> Describe()
    {
        Vector3 pos = Position;
        yield return $"Țintă          : ({Target.X,6:F2}, {Target.Y,6:F2}, {Target.Z,6:F2})";
        yield return $"Distanță       : {Distance,6:F2}";
        yield return $"Azimut / elev. : {Azimuth,7:F1}° / {Elevation,6:F1}°";
        yield return $"Poziție        : ({pos.X,6:F2}, {pos.Y,6:F2}, {pos.Z,6:F2})";
    }

    /// <summary>Bonus: așază ținta la o distanță fixă în fața camerei FPS, fără "salt" al imaginii.</summary>
    public void AttachToFps(FpsCamera fps, float lookDistance = 5.0f)
    {
        Target = fps.Position + fps.Front * lookDistance;
        Distance = lookDistance;

        // o = vectorul unitate tinta -> camera, adica -front
        Vector3 o = -fps.Front;
        Elevation = MathHelper.RadiansToDegrees(MathF.Asin(o.Y));
        Azimuth = MathHelper.RadiansToDegrees(MathF.Atan2(o.Z, o.X));
    }
}   