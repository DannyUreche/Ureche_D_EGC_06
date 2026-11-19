// Varianta B: evitarea obstacolelor

using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Tema (varianta B): jucatorul sta pe loc, pe una din 3 benzi, iar traficul (masini
/// "zburatoare", fara roti, si camioane) vine spre el. Stanga/dreapta schimba banda lin
/// (Tween, EaseOut, sub 0.2 s). Space = salt cu gravitate (sectiunea 5.4), insotit de un
/// mic efect de "abur" (ca o decolare). Masinile (joase) se sar, camioanele (inalte)
/// trebuie ocolite prin schimbarea benzii. Coliziunea foloseste un test AABB pe toate cele
/// 3 axe. Viteza traficului si frecventa aparitiilor cresc in timp.
///
/// Scorul pe ecran: motorul din laboratoarele 1-6 nu are niciun sistem de randare
/// text/HUD (nicio textura, niciun font, doar triunghiuri colorate). De aceea scorul este
/// construit ca un AFISAJ DIGITAL cu 7 segmente, din cuburi, ancorat intr-un punct fix din
/// lume, calculat o singura data din camera fixa a scenei: cum camera nu se misca niciodata
/// aici, afisajul ramane "lipit" vizual de coltul ecranului.
///
/// Flori vizuale suplimentare (fara efect asupra jocului):
///  - drum solid cu iarba pe margini si linii de banda intre benzi (ca pe un drum real);
///  - fiecare vehicul = caroserie (parinte) + acoperis/cabina + faruri + stopuri (copii);
///  - felinare pe margine (stalp + bec = alta ierarhie parinte-copil);
///  - jucatorul e un mic personaj: corp -> cap (copil) -> doi ochi (copii ai capului),
///    o ierarhie pe 3 niveluri, care se apleaca in viraje si se intinde/turteste la salt.
/// </summary>
public sealed class DodgeScene : Scene
{
    private const string Restart = "reporneste jocul";

    private static readonly float[] LaneX = { -2.2f, 0.0f, 2.2f };
    private const float PlayerZ = 4.0f;          // jucatorul sta fix pe aceasta adancime
    private const float GroundY = 0.5f;          // inaltimea "logica" a centrului jucatorului
    private const float Gravity = 18.0f;
    private const float JumpSpeed = 8.0f;
    private const float LaneChangeDuration = 0.18f;   // < 0.2 s, cum cere enuntul

    private const float BaseSpeed = 5.0f;
    private const float MaxSpeed = 16.0f;
    private const float SpeedGrowth = 0.12f;     // unitati/s, pentru fiecare secunda supravietuita

    private const float BaseSpawnInterval = 1.4f;
    private const float MinSpawnInterval = 0.55f;
    private const float SpawnShrink = 0.015f;

    // Cutia de coliziune a jucatorului este FIXA (nu depinde de scara vizuala, care se
    // schimba cosmetic la salt): asa hitbox-ul ramane corect si previzibil pentru joc.
    private static readonly Vector3 PlayerHalfExtents = new(0.45f, 0.5f, 0.45f);

    // Cutiile de coliziune ale vehiculelor (logice; pot diferi de dimensiunile vizuale exacte).
    private const float ObstacleHalfX = 0.5f, ObstacleHalfZ = 0.85f;
    private const float LowHalfY = 0.3f, LowCenterY = 0.3f;     // masina: se poate sari
    private const float TallHalfY = 1.0f, TallCenterY = 0.95f;  // camion: trebuie ocolit
    private const float SpawnZ = -36.0f;
    private const float DespawnZ = PlayerZ + 2.0f;

    private const int MarkersPerDivider = 10;
    private const float MarkerSpacing = 4.0f;

    // Decor static (drum, iarba, felinare).
    private const float RoadHalfWidth = 3.3f;
    private const float ShoulderWidth = 2.6f;
    private const float RoadCenterZ = -15.0f;
    private const float RoadLength = 54.0f;
    private const float PoleHeight = 3.2f;
    private static readonly float[] LampZ = { -30.0f, -22.0f, -14.0f, -6.0f, 2.0f };

    private static readonly Color4[] CarColors =
    {
        new(0.85f, 0.15f, 0.15f, 1.0f),
        new(0.15f, 0.35f, 0.85f, 1.0f),
        new(0.95f, 0.75f, 0.10f, 1.0f),
        new(0.20f, 0.75f, 0.35f, 1.0f),
    };
    private static readonly Color4[] TruckColors =
    {
        new(0.55f, 0.55f, 0.60f, 1.0f),
        new(0.75f, 0.35f, 0.10f, 1.0f),
        new(0.25f, 0.30f, 0.55f, 1.0f),
    };

    // --- Afișajul digital al scorului (7 segmente, din cuburi) -------------------------------

    private const float SegThickness = 0.09f;
    private const float SegLongLen = 0.30f;
    private const float SegDepth = 0.04f;
    private const float DigitSpacing = 0.46f;
    private const float HudDistance = 7.0f;      // cât de departe de cameră, pe direcția privirii
    private const float HudRight = 0.0f;         // deplasare spre stânga ecranului (colț)
    private const float HudUp =2.4f;           // deplasare spre susul ecranului

    private static readonly (float x, float y, float w, float h)[] SegmentLayout =
    {
        (0.0f, 0.42f, SegLongLen, SegThickness),     // a: sus
        (0.17f, 0.21f, SegThickness, SegLongLen),    // b: sus-dreapta
        (0.17f, -0.21f, SegThickness, SegLongLen),   // c: jos-dreapta
        (0.0f, -0.42f, SegLongLen, SegThickness),    // d: jos
        (-0.17f, -0.21f, SegThickness, SegLongLen),  // e: jos-stânga
        (-0.17f, 0.21f, SegThickness, SegLongLen),   // f: sus-stânga
        (0.0f, 0.0f, SegLongLen, SegThickness),      // g: mijloc
    };

    // Care segmente (a..g) sunt aprinse pentru fiecare cifră 0-9.
    private static readonly bool[][] DigitSegments =
    {
        new[] { true, true, true, true, true, true, false },   // 0
        new[] { false, true, true, false, false, false, false }, // 1
        new[] { true, true, false, true, true, false, true },  // 2
        new[] { true, true, true, true, false, false, true },  // 3
        new[] { false, true, true, false, false, true, true }, // 4
        new[] { true, false, true, true, false, true, true },  // 5
        new[] { true, false, true, true, true, true, true },   // 6
        new[] { true, true, true, false, false, false, false },// 7
        new[] { true, true, true, true, true, true, true },    // 8
        new[] { true, true, true, true, false, true, true },   // 9
    };

    private SceneObject[,] _scoreSegments = null!;   // [cifră 0..2, segment a..g]

    // --- Jucătorul, obstacolele, aburul ------------------------------------------------------

    private readonly Random _rng = new();
    private Mesh _cubeMesh = null!;
    private Mesh _sphereMesh = null!;

    private SceneObject _player = null!;
    private readonly Tween _laneTween = new(Vector3.Zero, Vector3.Zero, 0.001f, Easing.EaseOut);
    private int _lane = 1;
    private float _verticalSpeed;
    private bool _grounded = true;
    private float _prevPlayerX;

    private enum State { Playing, GameOver }
    private State _state = State.Playing;

    private float _score;
    private float _record;
    private float _speed;
    private float _spawnTimer;

    private sealed class Obstacle
    {
        public SceneObject Body = null!;
        public readonly List<SceneObject> Parts = new();
        public bool Tall;
    }
    private readonly List<Obstacle> _obstacles = new();
    private readonly List<SceneObject> _markers = new();

    private sealed class Puff
    {
        public SceneObject Visual = null!;
        public Vector3 Velocity;
        public float Life;
        public float MaxLife;
    }
    private readonly List<Puff> _puffs = new();

    public override string Name => "evitare obstacole";

    public override IEnumerable<string> ActionNames =>
        new[] { Actions.Left, Actions.Right, Actions.Jump, Restart };

    public override string Status =>
        _state == State.Playing
            ? $"bandă {_lane + 1}/3 | scor {_score:F1} s | record {_record:F1} s"
            : $"GAME OVER | scor {_score:F1} s | record {_record:F1} s | {Restart}";

    public override void Load(Input input)
    {
        CameraPosition = new Vector3(0.0f, 3.2f, PlayerZ + 5.0f);
        CameraTarget = new Vector3(0.0f, 1.0f, PlayerZ - 18.0f);

        // Acțiuni prin NUME, nicio tastă nu se citește direct din KeyboardState.
        input.Bind(Actions.Left, Keys.A, Keys.Left);
        input.Bind(Actions.Right, Keys.D, Keys.Right);
        input.Bind(Actions.Jump, Keys.Space);
        input.Bind(Restart, Keys.R);

        _cubeMesh = AddMesh(Shapes.ShadedCube());
        _sphereMesh = AddMesh(Shapes.ShadedSphere(1.0f, 12, 8));
        Mesh poleMesh = AddMesh(Shapes.ShadedCylinder(0.07f, PoleHeight, 10));

        BuildGround();
        BuildLampPosts(poleMesh);
        BuildLaneMarkings();
        BuildPlayer();
        BuildScoreHud();

        StartNewGame();
    }

    // --- Decor static: drum, iarbă, felinare, linii de bandă --------------------------------

    private void BuildGround()
    {
        SceneObject road = AddObject("drum", _cubeMesh);
        road.Transform.Scale = new Vector3(RoadHalfWidth * 2.0f, 0.1f, RoadLength);
        road.Transform.Position = new Vector3(0.0f, -0.05f, RoadCenterZ);
        road.Tint = new Color4(0.22f, 0.23f, 0.27f, 1.0f);

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            SceneObject shoulder = AddObject("iarbă", _cubeMesh);
            shoulder.Transform.Scale = new Vector3(ShoulderWidth, 0.1f, RoadLength);
            shoulder.Transform.Position =
                new Vector3(side * (RoadHalfWidth + ShoulderWidth / 2.0f), -0.05f, RoadCenterZ);
            shoulder.Tint = new Color4(0.18f, 0.32f, 0.2f, 1.0f);
        }
    }

    /// <summary>Felinare pe marginea drumului: fiecare e o ierarhie stâlp (părinte) + bec (copil).</summary>
    private void BuildLampPosts(Mesh poleMesh)
    {
        foreach (float z in LampZ)
        {
            foreach (float side in new[] { -1.0f, 1.0f })
            {
                float x = side * (RoadHalfWidth + 0.6f);

                SceneObject post = AddObject("stâlp", poleMesh);
                post.Transform.Position = new Vector3(x, PoleHeight / 2.0f, z);
                post.Tint = new Color4(0.35f, 0.36f, 0.4f, 1.0f);

                SceneObject lamp = AddObject("stâlp:bec", _sphereMesh, post);
                lamp.Transform.Scale = new Vector3(0.5f);
                lamp.Transform.Position = new Vector3(0.0f, PoleHeight / 2.0f + 0.25f, 0.0f);
                lamp.Tint = new Color4(1.0f, 0.85f, 0.45f, 1.0f);
            }
        }
    }

    /// <summary>Liniile de bandă (întrerupte) stau ÎNTRE benzi, ca pe un drum real, nu sub vehicule.</summary>
    private void BuildLaneMarkings()
    {
        float dividerX1 = (LaneX[0] + LaneX[1]) / 2.0f;
        float dividerX2 = (LaneX[1] + LaneX[2]) / 2.0f;

        foreach (float x in new[] { dividerX1, dividerX2 })
        {
            for (int k = 0; k < MarkersPerDivider; k++)
            {
                SceneObject marker = AddObject("linie bandă", _cubeMesh);
                marker.Transform.Scale = new Vector3(0.12f, 0.02f, 0.9f);
                marker.Transform.Position = new Vector3(x, 0.02f, SpawnZ + k * MarkerSpacing);
                marker.Tint = new Color4(0.92f, 0.92f, 0.85f, 1.0f);
                _markers.Add(marker);
            }
        }
    }

    /// <summary>Jucătorul: corp -> cap (copil) -> doi ochi (copii ai capului): ierarhie pe 3 niveluri.</summary>
    private void BuildPlayer()
    {
        _player = AddObject("jucător", _cubeMesh);

        SceneObject head = AddObject("jucător:cap", _cubeMesh, _player);
        head.Transform.Scale = new Vector3(0.55f, 0.5f, 0.55f);
        head.Transform.Position = new Vector3(0.0f, 0.62f, -0.15f);
        head.Tint = new Color4(0.95f, 0.93f, 0.85f, 1.0f);

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            SceneObject eye = AddObject("jucător:ochi", _sphereMesh, head);
            eye.Transform.Scale = new Vector3(0.16f);
            eye.Transform.Position = new Vector3(side * 0.18f, 0.05f, -0.52f);
            eye.Tint = new Color4(0.05f, 0.05f, 0.05f, 1.0f);
        }
    }

    /// <summary>
    /// Afișajul scorului: un punct fix (calculat din camera, fixă în această scenă), cu o
    /// ierarhie pe 3 niveluri: panou (HUD) -> cifră (0, 1, 2) -> segment (a..g).
    /// </summary>
    private void BuildScoreHud()
    {
        Vector3 forward = Vector3.Normalize(CameraTarget - CameraPosition);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));

        Vector3 hudPosition = CameraPosition + forward * HudDistance - right * HudRight + up * HudUp;

        SceneObject hudRoot = AddObject("scor:panou", null);
        hudRoot.Transform.Position = hudPosition;

        _scoreSegments = new SceneObject[3, 7];
        for (int digit = 0; digit < 3; digit++)
        {
            SceneObject digitRoot = AddObject($"scor:cifră{digit}", null, hudRoot);
            digitRoot.Transform.Position = new Vector3(digit * DigitSpacing, 0.0f, 0.0f);

            for (int seg = 0; seg < 7; seg++)
            {
                (float x, float y, float w, float h) = SegmentLayout[seg];
                SceneObject segment = AddObject($"scor:{digit}:{seg}", _cubeMesh, digitRoot);
                segment.Transform.Position = new Vector3(x, y, 0.0f);
                segment.Transform.Scale = new Vector3(w, h, SegDepth);
                segment.Tint = new Color4(0.95f, 0.95f, 0.98f, 1.0f);   // alb, pentru lizibilitate
                _scoreSegments[digit, seg] = segment;
            }
        }
    }

    // --- Bucla de joc -----------------------------------------------------------------------

    public override void Update(float dt, Input input)
    {
        if (input.WasPressed(Restart))
        {
            StartNewGame();
        }
        else if (_state == State.Playing)
        {
            UpdatePlaying(dt, input);
        }

        UpdateLaneMarkings(dt);
        UpdatePuffs(dt);
        UpdateScoreHud();
        base.Update(dt, input);   // rotește obiectele cu Spin (dacă vor exista)
    }

    private void UpdatePlaying(float dt, Input input)
    {
        _score += dt;
        _speed = MathF.Min(MaxSpeed, BaseSpeed + SpeedGrowth * _score);

        // 1. Schimbarea benzii: apăsare (nu ținere), animată lin cu Tween + EaseOut.
        if (input.WasPressed(Actions.Left) && _lane > 0)
        {
            _lane--;
            _laneTween.GoTo(new Vector3(LaneX[_lane], 0.0f, 0.0f), LaneChangeDuration);
        }
        if (input.WasPressed(Actions.Right) && _lane < LaneX.Length - 1)
        {
            _lane++;
            _laneTween.GoTo(new Vector3(LaneX[_lane], 0.0f, 0.0f), LaneChangeDuration);
        }
        _laneTween.Update(dt);
        float playerX = _laneTween.Value.X;

        // 2. Saltul cu gravitate (Euler semi-implicit, secțiunea 5.4), cu un efect de "abur".
        if (_grounded && input.WasPressed(Actions.Jump))
        {
            _verticalSpeed = JumpSpeed;
            _grounded = false;
            SpawnJumpEffect(_player.Transform.Position);
        }
        float playerY = _player.Transform.Position.Y;
        if (!_grounded)
        {
            _verticalSpeed -= Gravity * dt;
            playerY += _verticalSpeed * dt;
            if (playerY <= GroundY)
            {
                playerY = GroundY;
                _verticalSpeed = 0.0f;
                _grounded = true;
            }
        }
        _player.Transform.Position = new Vector3(playerX, playerY, PlayerZ);

        // 3. Flori vizuale pe jucător: lean în viraj, squash & stretch la salt, culoare
        //    ce ciclează ușor în timp. Capul și ochii urmează automat (sunt copii).
        float lean = Math.Clamp((playerX - _prevPlayerX) / MathF.Max(dt, 0.0001f) * 2.2f, -16.0f, 16.0f);
        _prevPlayerX = playerX;

        float stretch = _grounded ? 0.0f : Math.Clamp(_verticalSpeed / JumpSpeed, -0.6f, 0.6f);
        _player.Transform.Scale = new Vector3(0.8f - 0.12f * stretch, 0.8f + 0.28f * stretch, 0.8f - 0.12f * stretch);
        _player.Transform.Rotation = new Vector3(0.0f, 0.0f, -lean);
        _player.Tint = ColorUtil.FromHsv((_score * 0.05f) % 1.0f, 0.55f, 1.0f);

        // 4. Traficul: avans spre jucător, testul AABB, eliminarea vehiculelor trecute.
        var playerCenter = new Vector3(playerX, playerY, PlayerZ);
        for (int i = _obstacles.Count - 1; i >= 0; i--)
        {
            Obstacle obstacle = _obstacles[i];
            Vector3 pos = obstacle.Body.Transform.Position;
            pos.Z += _speed * dt;
            obstacle.Body.Transform.Position = pos;

            float centerY = obstacle.Tall ? TallCenterY : LowCenterY;
            float halfY = obstacle.Tall ? TallHalfY : LowHalfY;
            var obstacleCenter = new Vector3(pos.X, centerY, pos.Z);
            var obstacleHalf = new Vector3(ObstacleHalfX, halfY, ObstacleHalfZ);

            if (Overlap(playerCenter, PlayerHalfExtents, obstacleCenter, obstacleHalf))
            {
                _state = State.GameOver;
                _record = MathF.Max(_record, _score);
                ConsolePanel.PrintMessage($"coliziune! scor {_score:F1} s (record {_record:F1} s)");
                break;
            }

            if (pos.Z > DespawnZ)
            {
                RemoveObstacle(obstacle);
                _obstacles.RemoveAt(i);
            }
        }

        // 5. Generarea de vehicule noi, din ce în ce mai dese.
        _spawnTimer -= dt;
        if (_spawnTimer <= 0.0f)
        {
            SpawnObstacle();
            _spawnTimer = MathF.Max(MinSpawnInterval, BaseSpawnInterval - SpawnShrink * _score);
        }
    }

    private void UpdateLaneMarkings(float dt)
    {
        if (_state != State.Playing) return;
        foreach (SceneObject marker in _markers)
        {
            Vector3 pos = marker.Transform.Position;
            pos.Z += _speed * dt;
            if (pos.Z > DespawnZ)
            {
                pos.Z -= MarkersPerDivider * MarkerSpacing;
            }
            marker.Transform.Position = pos;
        }
    }

    /// <summary>Scorul curent (secunde întregi, 0-999), afișat pe panoul cu 7 segmente.</summary>
    private void UpdateScoreHud()
    {
        int totalSeconds = Math.Clamp((int)MathF.Floor(_score), 0, 999);
        int[] values = { totalSeconds / 100, (totalSeconds / 10) % 10, totalSeconds % 10 };

        for (int digit = 0; digit < 3; digit++)
        {
            bool[] segmentsOn = DigitSegments[values[digit]];
            for (int seg = 0; seg < 7; seg++)
            {
                (float x, float y, float w, float h) = SegmentLayout[seg];
                _scoreSegments[digit, seg].Transform.Scale =
                    segmentsOn[seg] ? new Vector3(w, h, SegDepth) : Vector3.Zero;
            }
        }
    }

    /// <summary>Un vehicul = caroseria (părinte, se mișcă) + acoperiș/cabină + faruri + stopuri (copii).</summary>
    private void SpawnObstacle()
    {
        int lane = _rng.Next(LaneX.Length);
        bool tall = _rng.NextDouble() < 0.4;
        var obstacle = new Obstacle { Tall = tall };

        if (tall)
        {
            // "Camion": caroserie înaltă, lungă; cabina stă în față, pe direcția de mers (+Z).
            SceneObject body = AddObject("camion", _cubeMesh);
            body.Transform.Scale = new Vector3(0.95f, 1.7f, 1.6f);
            body.Transform.Position = new Vector3(LaneX[lane], 0.85f, SpawnZ);
            body.Tint = TruckColors[_rng.Next(TruckColors.Length)];
            obstacle.Body = body;

            SceneObject cabin = AddObject("camion:cabină", _cubeMesh, body);
            cabin.Transform.Scale = new Vector3(0.9f, 0.55f, 0.35f);
            cabin.Transform.Position = new Vector3(0.0f, 0.62f, 0.55f);
            cabin.Tint = new Color4(0.75f, 0.85f, 0.92f, 1.0f);
            obstacle.Parts.Add(cabin);

            AddLights(obstacle, body, 0.42f, 0.78f, 0.07f);
        }
        else
        {
            // "Tesla zburătoare": caroserie joasă și lungă, FĂRĂ roți, acoperiș cu sticlă
            // fumurie (ușor spre spate) și o bandă luminoasă dedesubt, ca o plutire.
            // Copiii (acoperiș, bandă) au poziția/mărimea compensate la scara caroseriei
            // (care e puternic ne-uniformă), ca să iasă exact cum trebuie.
            SceneObject body = AddObject("mașină", _cubeMesh);
            Vector3 bodyScale = new(0.82f, 0.32f, 1.78f);
            body.Transform.Scale = bodyScale;
            body.Transform.Position = new Vector3(LaneX[lane], 0.3f, SpawnZ);
            body.Tint = CarColors[_rng.Next(CarColors.Length)];
            obstacle.Body = body;

            SceneObject roof = AddObject("mașină:acoperiș", _cubeMesh, body);
            roof.Transform.Position = Compensate(new Vector3(0.0f, 0.42f, -0.15f), bodyScale);
            roof.Transform.Scale = Compensate(new Vector3(0.5f, 0.26f, 0.78f), bodyScale);
            roof.Tint = new Color4(0.08f, 0.10f, 0.14f, 1.0f);   // sticlă fumurie
            obstacle.Parts.Add(roof);

            SceneObject glow = AddObject("mașină:levitație", _cubeMesh, body);
            glow.Transform.Position = Compensate(new Vector3(0.0f, -0.22f, 0.0f), bodyScale);
            glow.Transform.Scale = Compensate(new Vector3(0.9f, 0.04f, 1.85f), bodyScale);
            glow.Tint = new Color4(0.25f, 0.85f, 1.0f, 1.0f);   // bandă luminoasă, "levitație"
            obstacle.Parts.Add(glow);

            AddLights(obstacle, body, 0.3f, 0.5f, 0.05f);
        }

        _obstacles.Add(obstacle);
    }

    /// <summary>
    /// Pentru un copil al unui obiect cu scară NE-uniformă: împarte valoarea dorită (în
    /// unități "absolute") la scara părintelui, pe fiecare axă, astfel încât poziția sau
    /// mărimea finală, în lume, să fie cea intenționată, nu una distorsionată de părinte.
    /// </summary>
    private static Vector3 Compensate(Vector3 desired, Vector3 parentScale) =>
        new(desired.X / parentScale.X, desired.Y / parentScale.Y, desired.Z / parentScale.Z);

    /// <summary>Faruri albe spre direcția de mers (+Z) și stopuri roșii în spate (-Z), copii ai caroseriei.</summary>
    private void AddLights(Obstacle obstacle, SceneObject body, float sideX, float halfLengthZ, float radius)
    {
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            SceneObject headlight = AddObject("far", _sphereMesh, body);
            headlight.Transform.Scale = new Vector3(radius);
            headlight.Transform.Position = new Vector3(side * sideX, -0.15f, halfLengthZ);
            headlight.Tint = new Color4(1.0f, 0.98f, 0.85f, 1.0f);
            obstacle.Parts.Add(headlight);

            SceneObject taillight = AddObject("stop", _sphereMesh, body);
            taillight.Transform.Scale = new Vector3(radius);
            taillight.Transform.Position = new Vector3(side * sideX, -0.15f, -halfLengthZ);
            taillight.Tint = new Color4(0.9f, 0.1f, 0.1f, 1.0f);
            obstacle.Parts.Add(taillight);
        }
    }

    private void RemoveObstacle(Obstacle obstacle)
    {
        Objects.Remove(obstacle.Body);
        foreach (SceneObject part in obstacle.Parts)
        {
            Objects.Remove(part);
        }
    }

    /// <summary>La saltul jucătorului: câțiva "nori de abur" (sfere mici) pornesc de sub el,
    /// se extind și se ridică puțin, apoi dispar - ca o mică decolare.</summary>
    private void SpawnJumpEffect(Vector3 origin)
    {
        for (int i = 0; i < 6; i++)
        {
            float angle = (float)(_rng.NextDouble() * MathF.PI * 2.0);
            float spread = 0.15f + (float)_rng.NextDouble() * 0.25f;
            var velocity = new Vector3(
                MathF.Cos(angle) * spread,
                0.6f + (float)_rng.NextDouble() * 0.5f,
                MathF.Sin(angle) * spread);

            SceneObject puffVisual = AddObject("abur", _sphereMesh);
            puffVisual.Transform.Position = origin + new Vector3(0.0f, -0.15f, 0.0f);
            puffVisual.Transform.Scale = new Vector3(0.12f);
            puffVisual.Tint = new Color4(0.85f, 0.92f, 0.98f, 1.0f);

            _puffs.Add(new Puff { Visual = puffVisual, Velocity = velocity, Life = 0.45f, MaxLife = 0.45f });
        }
    }

    private void UpdatePuffs(float dt)
    {
        for (int i = _puffs.Count - 1; i >= 0; i--)
        {
            Puff puff = _puffs[i];
            puff.Life -= dt;
            if (puff.Life <= 0.0f)
            {
                Objects.Remove(puff.Visual);
                _puffs.RemoveAt(i);
                continue;
            }

            Vector3 pos = puff.Visual.Transform.Position + puff.Velocity * dt;
            puff.Visual.Transform.Position = pos;

            float t = 1.0f - puff.Life / puff.MaxLife;   // 0 la naștere, 1 la final
            float scale = 0.12f + 0.22f * t;             // crește treptat, ca un norișor
            puff.Visual.Transform.Scale = new Vector3(scale);
        }
    }

    /// <summary>Două cutii se suprapun dacă se suprapun pe toate cele trei axe.</summary>
    private static bool Overlap(Vector3 aCenter, Vector3 aHalf, Vector3 bCenter, Vector3 bHalf) =>
        MathF.Abs(aCenter.X - bCenter.X) < aHalf.X + bHalf.X &&
        MathF.Abs(aCenter.Y - bCenter.Y) < aHalf.Y + bHalf.Y &&
        MathF.Abs(aCenter.Z - bCenter.Z) < aHalf.Z + bHalf.Z;

    private void StartNewGame()
    {
        _state = State.Playing;
        _score = 0.0f;
        _speed = BaseSpeed;
        _spawnTimer = BaseSpawnInterval;
        _verticalSpeed = 0.0f;
        _grounded = true;
        _lane = 1;
        _prevPlayerX = LaneX[_lane];

        _laneTween.GoTo(new Vector3(LaneX[_lane], 0.0f, 0.0f), 0.001f);
        _laneTween.Update(1.0f);   // forțează instant poziția de start, înainte de primul cadru

        _player.Transform.Scale = new Vector3(0.8f);
        _player.Transform.Rotation = Vector3.Zero;
        _player.Tint = ColorUtil.FromHsv(0.0f, 0.55f, 1.0f);
        _player.Transform.Position = new Vector3(LaneX[_lane], GroundY, PlayerZ);

        foreach (Obstacle obstacle in _obstacles)
        {
            RemoveObstacle(obstacle);
        }
        _obstacles.Clear();
    }

    public override IEnumerable<string> Describe()
    {
        yield return $"Stare          : {(_state == State.Playing ? "joc" : "GAME OVER")}";
        yield return $"Bandă          : {_lane + 1} / {LaneX.Length}";
        yield return $"Scor / record  : {_score:F2} s / {_record:F2} s";
        yield return $"Viteză trafic  : {_speed:F2} unități/s";
        yield return $"Vehicule       : {_obstacles.Count}   (următorul în {_spawnTimer:F2} s)";
        yield return $"Jucător        : y = {_player.Transform.Position.Y:F2}   la sol: {(_grounded ? "da" : "nu")}";
    }
}