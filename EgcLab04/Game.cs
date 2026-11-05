using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Fereastra aplicației și bucla principală (main game loop).
/// Față de laboratorul 3: o scenă cu mai multe obiecte, fiecare cu matricea lui model,
/// o cameră cu matricele view și projection, tastatura și mouse-ul.
/// </summary>
public class Game : GameWindow
{
    // Obiecte care NU depind de OpenGL pot fi create imediat.
    private readonly Camera _camera = new();

    // Obiectele care folosesc OpenGL se creează în OnLoad (abia atunci există contextul OpenGL).
    private Shader _shader = null!;
    private readonly List<SceneObject> _scene = new();
    private readonly List<Transform2D> _initialTransforms = new();   // pentru tasta R

    // Starea aplicației (se modifică în OnUpdateFrame, se folosește în OnRenderFrame).
    private int _selected;            // indexul obiectului selectat în _scene (tasta Tab)
    private bool _wireframe;          // contur (true) sau plin (false)

    // Contoare afișate în titlul ferestrei.
    private readonly string _baseTitle;
    private double _secondTimer;
    private int _frames, _lastFrames;
    private bool _titleDirty = true;
    private Vector2 _pivot = Vector2.Zero;   // T4: punctul din lume stabilit cu clic dreapta
        private bool _hasPivot;                  // T4: dacă a fost stabilit vreun pivot
    private SceneObject _satellite = null!;   // T5: satelitul, desenat separat de _scene
    private float _orbitAngle;                 // T5: unghiul curent al orbitei, în grade

    public Game(int width, int height, string title)
        : base(GameWindowSettings.Default,
               new NativeWindowSettings
               {
                   ClientSize = new Vector2i(width, height),
                   Title = title,
                   APIVersion = new Version(3, 3),          // cerem OpenGL 3.3 ...
                   Profile = ContextProfile.Core,           // ... profil Core (fără modul imediat)
                   Flags = ContextFlags.ForwardCompatible
               })
    {
        _baseTitle = title;
        VSync = VSyncMode.On;   // swap-ul bufferelor așteaptă reîmprospătarea monitorului
    }

    /// <summary>Apelată O SINGURĂ DATĂ, după crearea ferestrei și a contextului OpenGL.</summary>
    protected override void OnLoad()
    {
        base.OnLoad();

        Console.WriteLine($"OpenGL  : {GL.GetString(StringName.Version)}");
        Console.WriteLine($"Renderer: {GL.GetString(StringName.Renderer)}");

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        _camera.Resize(ClientSize.X, ClientSize.Y);

        _shader = Shader.FromFiles("Shaders/shader.vert", "Shaders/shader.frag");

        // Scena: aceleași forme din laboratorul 3, acum plasate în lume prin Transform2D.
        // Depth (z) contează doar în perspectivă (tasta O, secțiunea 7.2).
        AddObject("pătrat", Shapes.Quad(),
                  new Transform2D { Position = new Vector2(-1.8f, -1.0f), Depth = -3.0f });
        AddObject("triunghi", Shapes.Triangle(),
                  new Transform2D { Position = new Vector2(-1.2f, 1.0f) });
        
        AddObject("hexagon", Shapes.RegularPolygon(6, 0.5f),
                  new Transform2D { Position = new Vector2(2.0f, 1.0f) });
        // T5: satelitul - un triunghi mic, desenat separat (nu intra in _scene, ca Tab sa nu-l selecteze)
        _satellite = new SceneObject("satelit", Shapes.Triangle(), new Transform2D { Scale = new Vector2(0.3f, 0.3f) });

        /*     //ex 2.1 (comentam randurile de mai sus 69 si 70 si decomentam randurile 73 si 74)
         AddObject("hexagon", Shapes.RegularPolygon(6, 0.5f),
            new Transform2D { Position = new Vector2(3.0f, 2.0f) });*/
        AddObject("cerc", Shapes.RegularPolygon(64, 0.5f),
                  new Transform2D { Position = new Vector2(0.8f, -0.6f), Depth = 2.0f });
         //cod ex4.1
        var p = new Vector4(2, 1, 0, 1);    // punctul (2; 1), cu a patra coordonată 1
        Console.WriteLine(p * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(90)));
        Console.WriteLine(p * Matrix4.CreateScale(-1, 1, 1));
        Console.WriteLine(p * Matrix4.CreateTranslation(3, -2, 0));
        Console.WriteLine(Matrix4.CreateTranslation(3, -2, 0));

        //cod ex5.1
        var q = new Vector4(1, 0, 0, 1);
        Matrix4 s = Matrix4.CreateScale(2);
        Matrix4 t = Matrix4.CreateTranslation(3, 0, 0);
        Console.WriteLine(q * (s * t));
        Console.WriteLine(q * (t * s));

        Matrix4 pivotRotation = Matrix4.CreateTranslation(-1, 0, 0)
                       * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(90))
                       * Matrix4.CreateTranslation(1, 0, 0);
        var pivotPoint = new Vector4(2, 1, 0, 1);
        Console.WriteLine(pivotPoint * pivotRotation);

    }

    /// <summary>Adaugă un obiect în scenă și reține transformarea lui inițială (pentru tasta R).</summary>
    private void AddObject(string name, Mesh mesh, Transform2D transform)
    {
        _scene.Add(new SceneObject(name, mesh, transform));
        _initialTransforms.Add(transform.Clone());
    }

    /// <summary>Apelată când se schimbă dimensiunea ferestrei.</summary>
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        GL.Viewport(0, 0, e.Width, e.Height);   // zona ferestrei în care se desenează
        _camera.Resize(e.Width, e.Height);      // proiecția se adaptează la noul raport de aspect
    }
 
    /// <summary>Apelată la fiecare iterație a buclei: LOGICA aplicației (intrări, mișcare, timp).</summary>
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
        }

        // --- Obiectul selectat: tastatura (secțiunea 8) ------------------------------

        float dt = (float)args.Time;
        SceneObject sel = _scene[_selected];
        /*
                const float moveSpeed = 2.0f;   // unități de lume pe secundă
                if (KeyboardState.IsKeyDown(Keys.Left))  sel.Transform.Position.X -= moveSpeed * dt;
                if (KeyboardState.IsKeyDown(Keys.Right)) sel.Transform.Position.X += moveSpeed * dt;
                if (KeyboardState.IsKeyDown(Keys.Down))  sel.Transform.Position.Y -= moveSpeed * dt;
                if (KeyboardState.IsKeyDown(Keys.Up))    sel.Transform.Position.Y += moveSpeed * dt;
        */
        // TODO (T2): Q / E rotesc obiectul, Z / X îl scalează

        // T2: scalarea se face prin inmultire, nu prin adunare, pentru ca o scalare prin
        // adunare (de exemplu -0.5 pe secunda) ar ajunge la 0 si apoi la valori negative,
        // care oglindesc forma in loc sa o micsoreze. Prin inmultire, scara se apropie de
        // 0 fara sa-l atinga niciodata, iar marirea si micsorarea raman simetrice
        // (un factor de 1.5 mareste la fel de mult pe cat il micsoreaza factorul sau invers, 1/1.5).

        const float moveSpeed = 2.0f;   // unități de lume pe secundă
        float moveFactor = KeyboardState.IsKeyDown(Keys.LeftShift) ? 3.0f : 1.0f;
        if (KeyboardState.IsKeyDown(Keys.Left)) sel.Transform.Position.X -= moveSpeed * moveFactor * dt;
        if (KeyboardState.IsKeyDown(Keys.Right)) sel.Transform.Position.X += moveSpeed * moveFactor * dt;
        if (KeyboardState.IsKeyDown(Keys.Down)) sel.Transform.Position.Y -= moveSpeed * moveFactor * dt;
        if (KeyboardState.IsKeyDown(Keys.Up)) sel.Transform.Position.Y += moveSpeed * moveFactor * dt;

        // T2: factorul de viteză (Shift ținut apăsat = 3x mai rapid)
        float speedFactor = KeyboardState.IsKeyDown(Keys.LeftShift) ? 3.0f : 1.0f;

        // T2: Q / E rotesc obiectul cu 90 de grade pe secundă
        const float rotateSpeed = 90.0f;   // grade pe secundă
        if (KeyboardState.IsKeyDown(Keys.Q)) sel.Transform.Rotation += rotateSpeed * speedFactor * dt;
        if (KeyboardState.IsKeyDown(Keys.E)) sel.Transform.Rotation -= rotateSpeed * speedFactor * dt;

        // T2: Z / X scalează obiectul cu un factor de 1.5 pe secundă (prin inmultire, nu adunare)
        const float scaleSpeed = 1.5f;     // factor pe secundă
        if (KeyboardState.IsKeyDown(Keys.Z))
        {
            float factor = MathF.Pow(1.0f / scaleSpeed, speedFactor * dt);
            sel.Transform.Scale *= factor;
        }
        if (KeyboardState.IsKeyDown(Keys.X))
        {
            float factor = MathF.Pow(scaleSpeed, speedFactor * dt);
            sel.Transform.Scale *= factor;
        }
        sel.Transform.Scale = new Vector2(
            Math.Clamp(sel.Transform.Scale.X, 0.1f, 10.0f),
            Math.Clamp(sel.Transform.Scale.Y, 0.1f, 10.0f));

        if (KeyboardState.IsKeyPressed(Keys.Tab)) _selected = (_selected + 1) % _scene.Count;
        if (KeyboardState.IsKeyPressed(Keys.O))   _camera.IsPerspective = !_camera.IsPerspective;
        if (KeyboardState.IsKeyPressed(Keys.F))   _wireframe = !_wireframe;
        if (KeyboardState.IsKeyPressed(Keys.R))   ResetScene();

        if (KeyboardState.IsKeyPressed(Keys.Tab) || KeyboardState.IsKeyPressed(Keys.O) ||
            KeyboardState.IsKeyPressed(Keys.R))
        {
            _titleDirty = true;
        }

        // --- Obiectul selectat: mouse-ul (secțiunea 9) -------------------------------

        // TODO (T3): la clic stânga, obiectul selectat se mută în punctul din lume aflat sub cursor
        //            (folosiți ScreenToWorld; Depth nu se schimbă)
        if (MouseState.IsButtonPressed(MouseButton.Left))
        {
            Vector2 worldPoint = ScreenToWorld(MouseState.Position);
            sel.Transform.Position = worldPoint;   // Depth nu se schimba
        }
        // TODO (T4, facultativ): clic dreapta = pivot; tasta P ținută = rotație în jurul pivotului
       
                                             // --- Camera: tastatura și rotița (secțiunea 6.2) -----------------------------
        // T4: clic dreapta stabileste un pivot in punctul din lume aflat sub cursor
if (MouseState.IsButtonPressed(MouseButton.Right))
{
    _pivot = ScreenToWorld(MouseState.Position);
    _hasPivot = true;
}

// T4: cat timp P este tinuta apasata, obiectul selectat se roteste in jurul pivotului
if (_hasPivot && KeyboardState.IsKeyDown(Keys.P))
{
    float pivotRotateSpeed = 90.0f * speedFactor * dt;   // grade, in acest cadru (respecta si Shift)
float rad = MathHelper.DegreesToRadians(pivotRotateSpeed);

// Rotim pozitia obiectului in jurul pivotului (sectiunea 5.2: aducem pivotul
// in origine, rotim, apoi il ducem inapoi la locul lui).
Vector2 relative = sel.Transform.Position - _pivot;
float cos = MathF.Cos(rad);
float sin = MathF.Sin(rad);
Vector2 rotated = new Vector2(
    relative.X * cos - relative.Y * sin,
    relative.X * sin + relative.Y * cos);
sel.Transform.Position = _pivot + rotated;

    // Unghiul propriu creste cu aceeasi valoare, ca obiectul sa "priveasca" mereu
    // spre pivot in acelasi fel, ca Luna fata de Pamant.
    sel.Transform.Rotation += pivotRotateSpeed;
}
    float camSpeed = 3.0f / _camera.Zoom;                    // unități de lume pe secundă
        if (KeyboardState.IsKeyDown(Keys.A)) _camera.Position.X -= camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.D)) _camera.Position.X += camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.S)) _camera.Position.Y -= camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.W)) _camera.Position.Y += camSpeed * dt;

        _camera.Zoom *= MathF.Pow(1.1f, MouseState.ScrollDelta.Y);   // un pas al rotiței = ±10 %
        _camera.Zoom = Math.Clamp(_camera.Zoom, 0.1f, 10.0f);

        // --- Titlul ferestrei: obiectul selectat, proiecția, cadre pe secundă --------

        _secondTimer += args.Time;
        if (_secondTimer >= 1.0)
        {
            _lastFrames = _frames;
            _frames = 0;
            _secondTimer -= 1.0;
            _titleDirty = true;
        }

        if (_titleDirty)
        {
            string projection = _camera.IsPerspective ? "perspectivă" : "ortografică";
            Title = $"{_baseTitle} | selectat: {_scene[_selected].Name} | proiecție {projection} | FPS: {_lastFrames}";
            _titleDirty = false;
        }
        // T5: unghiul orbitei creste constant, o rotatie completa (360 de grade) la 4 secunde
        const float orbitSpeed = 360.0f / 4.0f;   // grade pe secunda
        _orbitAngle += orbitSpeed * dt;
    }

    /// <summary>Apelată la fiecare iterație a buclei: DESENAREA unui cadru.</summary>
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        _frames++;

        // 1. Ștergem imaginea veche din back buffer.
        GL.ClearColor(0.12f, 0.14f, 0.18f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        // 2. Starea de rasterizare și programul shader.
        SetWireframe(_wireframe);
        _shader.Use();

        // 3. View și projection sunt aceleași pentru toate obiectele: le trimitem o singură dată.
        _shader.SetMatrix4("uView", _camera.ViewMatrix);
        _shader.SetMatrix4("uProjection", _camera.ProjectionMatrix);

        // 4. Fiecare obiect: matricea lui model, nuanța, apoi desenarea.
        foreach (SceneObject obj in _scene)
        {
            _shader.SetMatrix4("uModel", obj.Transform.ModelMatrix);
            _shader.SetColor("uTint", obj.Tint);
            obj.Mesh.Draw();
        }

        // TODO (T5, facultativ): satelitul obiectului selectat
        // T5: satelitul orbiteaza obiectul selectat. Transformarea locala (scalare, translatie,
        // apoi rotatie cu unghiul orbitei) este inmultita cu matricea model a obiectului selectat,
        // astfel incat satelitul urmeaza automat obiectul cand acesta este mutat, rotit sau scalat.
        SceneObject selectedForSatellite = _scene[_selected];
        Matrix4 localOrbit = Matrix4.CreateScale(0.3f, 0.3f, 1.0f)
                            * Matrix4.CreateTranslation(1.2f, 0.0f, 0.0f)
                            * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(_orbitAngle));
        Matrix4 satelliteModel = localOrbit * selectedForSatellite.Transform.ModelMatrix;

        _shader.SetMatrix4("uModel", satelliteModel);
        _shader.SetColor("uTint", _satellite.Tint);
        _satellite.Mesh.Draw();
        // 5. Afișăm cadrul terminat: back buffer <-> front buffer.
        SwapBuffers();
    }

    /// <summary>Apelată O SINGURĂ DATĂ, la închiderea ferestrei: eliberăm resursele GPU.</summary>
    protected override void OnUnload()
    {
        foreach (SceneObject obj in _scene)
        {
            obj.Dispose();
        }
        _satellite.Dispose();   // T5
        _shader.Dispose();

        base.OnUnload();
    }


    // TODO (T3): pasul 1 (pixel → NDC), apoi pasul 2 (NDC → lume, cu inversa lui view * projection)
    /// <summary>Transformă poziția mouse-ului (pixeli, origine stânga-sus) în coordonate de lume.</summary>
    private Vector2 ScreenToWorld(Vector2 mousePixels)
    {
        // T3, pasul 1: pixel -> NDC. Axa y se inverseaza (mouse-ul are y in jos, NDC are y in sus,
        // de aceea apare semnul 1 - ... in formula pentru y, nu doar minus ca la x).
        float xNdc = 2.0f * mousePixels.X / ClientSize.X - 1.0f;
        float yNdc = 1.0f - 2.0f * mousePixels.Y / ClientSize.Y;

        // T3, pasul 2: NDC -> lume, cu inversa matricei view * projection
        Matrix4 inverseViewProjection = Matrix4.Invert(_camera.ViewMatrix * _camera.ProjectionMatrix);
        Vector4 worldPos = new Vector4(xNdc, yNdc, 0.0f, 1.0f) * inverseViewProjection;

        return new Vector2(worldPos.X, worldPos.Y);
    }
    /// <summary>Tasta R: obiectele revin la transformările inițiale, camera la starea inițială.</summary>
    private void ResetScene()
    {
        for (int i = 0; i < _scene.Count; i++)
        {
            _scene[i].Transform = _initialTransforms[i].Clone();
        }
        _camera.Reset();
    }

    /// <summary>
    /// Contur / plin. Dacă Visual Studio semnalează o eroare la primul parametru,
    /// folosiți aici tipul acceptat de versiunea voastră (vezi laboratorul 2, Problema 3):
    /// TriangleFace.FrontAndBack sau MaterialFace.FrontAndBack.
    /// </summary>
    private static void SetWireframe(bool wireframe)
    {
        GL.PolygonMode(TriangleFace.FrontAndBack, wireframe ? PolygonMode.Line : PolygonMode.Fill);
    }
}
