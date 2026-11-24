using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Tema laboratorului 7 (varianta C): o planetă procedurală.
///   - terenul (ocean / pământ / munte) și calotele polare vin din zgomot 3D (fbm) pe suprafața sferei
///     (planet.frag), ca să nu existe nicio cusătură la poli;
///   - norii formează un strat separat de zgomot, care se deplasează în timp peste planetă;
///   - tasta G regenerează continentele: un nou "sămânță" (uSeed) e ales aleator, iar tranziția
///     către el se face lin, printr-un Tween pe 1.5 secunde (nu o schimbare bruscă).
/// </summary>
public sealed class TemaScene : Scene
{
    private const string Regenerate = "planetă: continente noi";

    private Material _planet = null!;
    private Tween _seed = null!;
    private readonly Random _random = new();

    public override string Name => "planetă";

    public override IEnumerable<string> ActionNames => new[] { Regenerate };

    public override string Status => "tema: planetă procedurală";

    protected override void Load(Input input)
    {
        CameraPosition = new Vector3(0.0f, 0.3f, 3.0f);
        CameraTarget = Vector3.Zero;

        input.Bind(Regenerate, Keys.G);

        Mesh sphere = AddMesh(Shapes.ShadedSphere(1.0f, 64, 32));

        _planet = AddMaterial("planetă", "procedural.vert", "planet.frag")
            .Param("uScale", 0.5f, 6.0f, 2.2f)
            .Param("uCloudScale", 0.5f, 6.0f, 3.0f)
            .Param("uCloudSpeed", 0.0f, 1.0f, 0.08f)
            .Param("uCloudCoverage", 0.0f, 1.0f, 0.45f);

        _planet.Uniforms.Set("uColorOcean", new Vector3(0.05f, 0.18f, 0.45f));
        _planet.Uniforms.Set("uColorLand", new Vector3(0.20f, 0.45f, 0.15f));
        _planet.Uniforms.Set("uColorMountain", new Vector3(0.45f, 0.40f, 0.32f));
        _planet.Uniforms.Set("uColorPolar", new Vector3(0.92f, 0.94f, 0.97f));
        _planet.Uniforms.Set("uCloudColor", new Vector3(0.95f, 0.96f, 0.98f));

        Vector3 seed0 = RandomSeed();
        _seed = new Tween(seed0, seed0, 1.5f, Easing.EaseInOut);
        _planet.Uniforms.Set("uSeed", seed0);

        SceneObject planet = AddObject("planetă", sphere, _planet);
        planet.Spin = new Vector3(0.0f, 6.0f, 0.0f);
    }

    public override void Update(float dt, Input input)
    {
        base.Update(dt, input);

        if (input.WasPressed(Regenerate))
        {
            _seed.GoTo(RandomSeed(), 1.5f);
            ConsolePanel.PrintMessage("planetă: continente noi (tranziție 1.5 s)");
        }
        _seed.Update(dt);
        _planet.Uniforms.Set("uSeed", _seed.Value);
    }

    /// <summary>O sămânță nouă pentru zgomotul de teren: valori mari, ca regiunile eșantionate să nu semene.</summary>
    private Vector3 RandomSeed() =>
        new(_random.NextSingle() * 40.0f, _random.NextSingle() * 40.0f, _random.NextSingle() * 40.0f);

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;
        Vector3 s = _seed.Value;
        yield return $"Sămânță teren  : ({s.X:0.0}, {s.Y:0.0}, {s.Z:0.0})";
    }
}