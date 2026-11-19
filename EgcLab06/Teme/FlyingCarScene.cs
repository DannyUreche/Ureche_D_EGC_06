// Scenă suplimentară (nu face parte din cele 3 variante oficiale ale temei): o mașină
// zburătoare controlată de jucător, cu o sferă care orbitează permanent în jurul ei.

using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// O mașină zburătoare, cu control tip vehicul (nu dronă): A/D rotesc direcția (viraj),
/// W/S accelerează înainte/înapoi pe direcția în care "privește" mașina, iar Space/Ctrl
/// stânga urcă/coboară. Mișcarea e limitată la o zonă de zbor (marcată cu stâlpi de
/// avertizare) ca mașina să nu poată ieși din scenă. O sferă orbitează permanent în jurul
/// ei, ca la sistemul solar din P5*.
///
/// Ierarhie: nucleu (mișcat de input, fără formă, scară = 1)
///             -> caroserie (vizuală, scară neuniformă) -> parbriz, capotă, oglinzi,
///                eleron + suporți, bandă de levitație, faruri, stopuri
///             -> pivot de orbită (fără formă, Spin pe Y) -> sfera satelit
/// Pivotul de orbită e FRATE cu caroseria (nu copilul ei): altfel scara ei neuniformă
/// ar transforma orbita circulară într-o elipsă.
/// </summary>
public sealed class FlyingCarScene : Scene
{
    private const string Ascend = "urcă";
    private const string Descend = "coboară";
    private const string Sprint = "viteză mare";

    private const float MoveSpeed = 6.0f;        // unități de lume pe secundă (pe direcția mașinii)
    private const float SprintMultiplier = 2.0f;
    private const float TurnSpeed = 110.0f;      // grade pe secundă, cât timp se ține viraj
    private const float ReverseFactor = 0.5f;    // mersul înapoi e mai lent decât înainte

    private const float OrbitRadius = 1.9f;
    private const float OrbitSpinSpeed = 75.0f;  // grade pe secundă

    // Zona de zbor: mașina nu poate ieși din acest cub invizibil (marcat cu stâlpi).
    private const float Bound = 14.0f;
    private const float MinAltitude = 0.6f;
    private const float MaxAltitude = 9.0f;

    private SceneObject _carRoot = null!;
    private float _yaw;     // grade; 0 = spre +Z, ca farurile
    private float _time;    // timp total de joc, pentru animațiile decorative (plutire)

    private readonly List<(SceneObject Obj, float BaseY, float Phase)> _floatingOrbs = new();

    public override string Name => "mașină zburătoare";

    public override IEnumerable<string> ActionNames =>
        new[] { Actions.Forward, Actions.Back, Actions.Left, Actions.Right, Ascend, Descend, Sprint };

    public override string Status
    {
        get
        {
            Vector3 p = _carRoot.Transform.Position;
            return $"poziție ({p.X:F1}, {p.Y:F1}, {p.Z:F1}) | direcție {_yaw:F0}°";
        }
    }

    public override void Load(Input input)
    {
        CameraPosition = new Vector3(0.0f, 8.0f, 18.0f);
        CameraTarget = new Vector3(0.0f, 1.5f, 0.0f);

        // Acțiuni prin NUME, nicio tastă nu se citește direct din KeyboardState.
        input.Bind(Actions.Forward, Keys.W, Keys.Up);
        input.Bind(Actions.Back, Keys.S, Keys.Down);
        input.Bind(Actions.Left, Keys.A, Keys.Left);
        input.Bind(Actions.Right, Keys.D, Keys.Right);
        input.Bind(Ascend, Keys.Space);
        input.Bind(Descend, Keys.LeftControl);
        input.Bind(Sprint, Keys.LeftShift);

        Mesh cubeMesh = AddMesh(Shapes.ShadedCube());
        Mesh sphereMesh = AddMesh(Shapes.ShadedSphere(1.0f, 16, 10));
        Mesh gridMesh = AddMesh(Shapes.Grid(16, 1.0f, new Color4(0.3f, 0.33f, 0.38f, 1.0f)));
        Mesh poleMesh = AddMesh(Shapes.ShadedCylinder(0.08f, 3.4f, 10));

        AddObject("podea", gridMesh);

        BuildBoundaryMarkers(poleMesh, sphereMesh);
        BuildLandmark(cubeMesh, sphereMesh);
      //  BuildFloatingOrbs(sphereMesh);
        BuildCar(cubeMesh, sphereMesh);
    }

    // --- Decor: limitele zonei de zbor, un turn central, sfere plutitoare -------------------

    /// <summary>Stâlpi de avertizare pe conturul zonei de zbor (colțuri + mijlocul fiecărei laturi).</summary>
    private void BuildBoundaryMarkers(Mesh poleMesh, Mesh sphereMesh)
    {
        var points = new (float x, float z)[]
        {
            (-Bound, -Bound), (Bound, -Bound), (-Bound, Bound), (Bound, Bound),
            (-Bound, 0.0f), (Bound, 0.0f), (0.0f, -Bound), (0.0f, Bound),
        };

        foreach ((float x, float z) in points)
        {
            SceneObject pole = AddObject("stâlp limită", poleMesh);
            pole.Transform.Position = new Vector3(x, 1.7f, z);
            pole.Tint = new Color4(0.5f, 0.12f, 0.16f, 1.0f);

            SceneObject beacon = AddObject("stâlp:semnal", sphereMesh, pole);
            beacon.Transform.Position = new Vector3(0.0f, 1.7f + 0.3f, 0.0f);
            beacon.Transform.Scale = new Vector3(0.35f);
            beacon.Tint = new Color4(1.0f, 0.25f, 0.3f, 1.0f);
        }
    }

    /// <summary>Un mic turn decorativ, ca reper de distanță și altitudine.</summary>
    private void BuildLandmark(Mesh cubeMesh, Mesh sphereMesh)
    {
        float[] sizes = { 2.2f, 1.6f, 1.1f, 0.7f };
        float y = 0.0f;
        var basePos = new Vector3(-8.0f, 0.0f, -8.0f);

        foreach (float size in sizes)
        {
            SceneObject block = AddObject("turn", cubeMesh);
            block.Transform.Scale = new Vector3(size, size * 0.8f, size);
            y += size * 0.8f / 2.0f;
            block.Transform.Position = basePos + new Vector3(0.0f, y, 0.0f);
            block.Tint = new Color4(0.3f, 0.32f, 0.4f, 1.0f);
            y += size * 0.8f / 2.0f;
        }

        SceneObject beacon = AddObject("turn:far", sphereMesh);
        beacon.Transform.Position = basePos + new Vector3(0.0f, y + 0.3f, 0.0f);
        beacon.Transform.Scale = new Vector3(0.4f);
        beacon.Tint = new Color4(0.3f, 0.9f, 1.0f, 1.0f);
    }
/*
    /// <summary>Câteva sfere colorate care plutesc ușor sus-jos, pur decorativ.</summary>
    private void BuildFloatingOrbs(Mesh sphereMesh)
    {
        var spots = new (float x, float z, float y)[]
        {
            (6.0f, -4.0f, 3.0f), (-5.0f, 7.0f, 4.5f), (8.0f, 6.0f, 2.2f),
            (-9.0f, -2.0f, 5.5f), (3.0f, 9.0f, 3.6f),
        };

        int i = 0;
        foreach ((float x, float z, float y) in spots)
        {
            SceneObject orb = AddObject("orb", sphereMesh);
            orb.Transform.Scale = new Vector3(0.5f);
            orb.Transform.Position = new Vector3(x, y, z);
            orb.Tint = ColorUtil.FromHsv(i / (float)spots.Length, 0.65f, 1.0f);
            _floatingOrbs.Add((orb, y, i * 1.3f));
            i++;
        }
    }
*/

    // --- Mașina -------------------------------------------------------------------------------

    /// <summary>Nucleul mișcat de input, cu caroseria vizuală și pivotul de orbită, ca frați.</summary>
    private void BuildCar(Mesh cubeMesh, Mesh sphereMesh)
    {
        _carRoot = AddObject("mașină:nucleu", null);
        _carRoot.Transform.Position = new Vector3(0.0f, 1.5f, 0.0f);

        // --- Caroseria (vizuală, scară ne-uniformă) ------------------------------------------
        Vector3 s = new(0.85f, 0.30f, 1.90f);   // scara caroseriei (folosită la Compensate)
        SceneObject body = AddObject("mașină:caroserie", cubeMesh, _carRoot);
        body.Transform.Scale = s;
        body.Tint = new Color4(0.78f, 0.1f, 0.16f, 1.0f);   // roșu-vișiniu metalic

        // Parbrizul "în trepte": piesa din spate (mai înaltă) + piesa din față (mai joasă),
        // ca să sugereze o pantă, fără o formă de pană propriu-zisă.
        SceneObject roof = AddObject("mașină:parbriz spate", cubeMesh, body);
        roof.Transform.Position = Compensate(new Vector3(0.0f, 0.40f, -0.35f), s);
        roof.Transform.Scale = Compensate(new Vector3(0.55f, 0.28f, 0.65f), s);
        roof.Tint = new Color4(0.08f, 0.10f, 0.14f, 1.0f);

        SceneObject windshield = AddObject("mașină:parbriz față", cubeMesh, body);
        windshield.Transform.Position = Compensate(new Vector3(0.0f, 0.26f, 0.15f), s);
        windshield.Transform.Scale = Compensate(new Vector3(0.6f, 0.20f, 0.55f), s);
        windshield.Tint = new Color4(0.1f, 0.12f, 0.16f, 1.0f);

        // Capota: o umflătură îngustă la vârful din față, pentru un aer mai "sportiv".
        SceneObject hood = AddObject("mașină:capotă", cubeMesh, body);
        hood.Transform.Position = Compensate(new Vector3(0.0f, 0.02f, 0.80f), s);
        hood.Transform.Scale = Compensate(new Vector3(0.55f, 0.22f, 0.35f), s);
        hood.Tint = body.Tint;

        // Oglinzile laterale.
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            SceneObject mirror = AddObject("mașină:oglindă", cubeMesh, body);
            mirror.Transform.Position = Compensate(new Vector3(side * 0.52f, 0.22f, 0.05f), s);
            mirror.Transform.Scale = Compensate(new Vector3(0.10f, 0.06f, 0.16f), s);
            mirror.Tint = new Color4(0.08f, 0.08f, 0.1f, 1.0f);
        }

        // Eleronul din spate, cu doi suporți subțiri.
        SceneObject spoiler = AddObject("mașină:eleron", cubeMesh, body);
        spoiler.Transform.Position = Compensate(new Vector3(0.0f, 0.46f, -0.85f), s);
        spoiler.Transform.Scale = Compensate(new Vector3(0.75f, 0.05f, 0.22f), s);
        spoiler.Tint = new Color4(0.05f, 0.05f, 0.06f, 1.0f);

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            SceneObject strut = AddObject("mașină:suport eleron", cubeMesh, body);
            strut.Transform.Position = Compensate(new Vector3(side * 0.28f, 0.34f, -0.85f), s);
            strut.Transform.Scale = Compensate(new Vector3(0.05f, 0.18f, 0.05f), s);
            strut.Tint = new Color4(0.05f, 0.05f, 0.06f, 1.0f);
        }

        // Banda de levitație (sugerează plutirea, fără roți).
        SceneObject glow = AddObject("mașină:levitație", cubeMesh, body);
        glow.Transform.Position = Compensate(new Vector3(0.0f, -0.20f, 0.0f), s);
        glow.Transform.Scale = Compensate(new Vector3(0.95f, 0.04f, 1.95f), s);
        glow.Tint = new Color4(0.35f, 0.9f, 1.0f, 1.0f);

        // Faruri (față) și stopuri/propulsoare (spate), mai mari decât la traficul obișnuit.
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            SceneObject headlight = AddObject("mașină:far", sphereMesh, body);
            headlight.Transform.Position = Compensate(new Vector3(side * 0.28f, -0.08f, 0.92f), s);
            headlight.Transform.Scale = Compensate(new Vector3(0.07f), s);
            headlight.Tint = new Color4(1.0f, 0.98f, 0.85f, 1.0f);

            SceneObject thruster = AddObject("mașină:propulsor", sphereMesh, body);
            thruster.Transform.Position = Compensate(new Vector3(side * 0.26f, -0.05f, -0.92f), s);
            thruster.Transform.Scale = Compensate(new Vector3(0.10f), s);
            thruster.Tint = new Color4(0.3f, 0.75f, 1.0f, 1.0f);
        }

        // --- Pivotul de orbită: FRATE cu caroseria, nu copilul ei (vezi comentariul clasei) ---
        SceneObject orbitPivot = AddObject("mașină:pivot orbită", null, _carRoot);
        orbitPivot.Spin = new Vector3(0.0f, OrbitSpinSpeed, 0.0f);

        SceneObject satellite = AddObject("satelit", sphereMesh, orbitPivot);
        satellite.Transform.Position = new Vector3(OrbitRadius, 0.0f, 0.0f);
        satellite.Transform.Scale = new Vector3(0.32f);
        satellite.Tint = new Color4(1.0f, 0.7f, 0.15f, 1.0f);
    }

    /// <summary>Compensează un copil al unui obiect cu scară NE-uniformă (vezi și DodgeScene).</summary>
    private static Vector3 Compensate(Vector3 desired, Vector3 parentScale) =>
        new(desired.X / parentScale.X, desired.Y / parentScale.Y, desired.Z / parentScale.Z);

    private static Vector3 Compensate(float uniform, Vector3 parentScale) =>
        Compensate(new Vector3(uniform), parentScale);

    // --- Bucla de joc -----------------------------------------------------------------------

    public override void Update(float dt, Input input)
    {
        _time += dt;

        // Virajul: A/D (sau săgeți) ROTESC direcția, ca la o mașină (volan), nu alunecă
        // lateral. Viteza unghiulară e constantă, indiferent dacă mașina merge sau stă.
        float turnInput = input.Axis(Actions.Right, Actions.Left);
        _yaw += turnInput * TurnSpeed * dt;

        // Mersul: W/S accelerează pe direcția în care "privește" mașina (yaw-ul curent);
        // mersul înapoi e mai lent, ca la o mașină reală.
        float forwardInput = input.Axis(Actions.Back, Actions.Forward);
        float yawRad = MathHelper.DegreesToRadians(_yaw);
        var facing = new Vector3(MathF.Sin(yawRad), 0.0f, MathF.Cos(yawRad));   // local +Z = fața mașinii

        float speedFactor = input.IsDown(Sprint) ? SprintMultiplier : 1.0f;
        float forwardSpeed = forwardInput >= 0.0f ? MoveSpeed : MoveSpeed * ReverseFactor;

        Vector3 horizontalMove = facing * forwardInput * forwardSpeed * speedFactor * dt;
        float verticalInput = input.Axis(Descend, Ascend);
        Vector3 verticalMove = new(0.0f, verticalInput * MoveSpeed * speedFactor * dt, 0.0f);

        Vector3 pos = _carRoot.Transform.Position + horizontalMove + verticalMove;

        // Zona de zbor: mașina nu poate ieși din acest cub invizibil (marcat cu stâlpi).
        pos.X = Math.Clamp(pos.X, -Bound, Bound);
        pos.Z = Math.Clamp(pos.Z, -Bound, Bound);
        pos.Y = Math.Clamp(pos.Y, MinAltitude, MaxAltitude);

        _carRoot.Transform.Position = pos;
        _carRoot.Transform.Rotation = new Vector3(0.0f, _yaw, 0.0f);
/*
        // Sferele decorative plutesc ușor sus-jos.
        foreach ((SceneObject obj, float baseY, float phase) in _floatingOrbs)
        {
            Vector3 p = obj.Transform.Position;
            p.Y = baseY + 0.4f * MathF.Sin(_time * 1.2f + phase);
            obj.Transform.Position = p;
        }
*/
        base.Update(dt, input);   // rotește pivotul de orbită (Spin) și tot ce mai are Spin
    }

    public override IEnumerable<string> Describe()
    {
        Vector3 p = _carRoot.Transform.Position;
        yield return $"Poziție mașină : ({p.X:F2}, {p.Y:F2}, {p.Z:F2})";
        yield return $"Direcție (yaw) : {_yaw:F1}°";
        yield return $"Zona de zbor   : X,Z în [-{Bound:F0}, {Bound:F0}]   Y în [{MinAltitude:F1}, {MaxAltitude:F1}]";
        yield return $"Rază orbită    : {OrbitRadius:F2}   viteză orbită: {OrbitSpinSpeed:F0}°/s";
    }
}