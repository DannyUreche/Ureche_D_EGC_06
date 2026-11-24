using OpenTK.Mathematics;

namespace Egc;

/// <summary>
/// NOU: o pistă de cadre-cheie (secțiunea 4): o listă de valori-cheie, la intervale EGALE de timp
/// (SecondsPerKey). Între două chei valoarea se interpolează. După ultima cheie pista revine
/// la prima și o ia de la capăt (bucla se închide singură).
/// </summary>
public sealed class KeyframeTrack
{
    private readonly List<Vector3> _keys = new();

    /// <summary>Timpul dintre două chei consecutive, în secunde.</summary>
    public float SecondsPerKey { get; }

    /// <summary>Easing-ul aplicat între două chei (implicit liniar).</summary>
    public EasingFunction Ease = Easing.Linear;

    public KeyframeTrack(float secondsPerKey)
    {
        SecondsPerKey = MathF.Max(secondsPerKey, 0.001f);
    }

    public int Count => _keys.Count;
    public IReadOnlyList<Vector3> Keys => _keys;

    /// <summary>Durata unui tur complet (inclusiv drumul de la ultima cheie înapoi la prima).</summary>
    public float Duration => _keys.Count * SecondsPerKey;

    /// <summary>Adaugă o cheie la sfârșitul pistei.</summary>
    public KeyframeTrack Add(Vector3 value)
    {
        _keys.Add(value);
        return this;   // permite scrierea în lanț: track.Add(...).Add(...)
    }

    public void Clear() => _keys.Clear();

    /// <summary>Valoarea pistei la momentul <paramref name="time"/> (în secunde).</summary>
    public Vector3 Sample(float time)
    {
        if (_keys.Count == 0) return Vector3.Zero;
        if (_keys.Count == 1) return _keys[0];

        // 6.3 (sectiunea 4.2):
        // 1. pozitia in numar de chei, adusa in [0, Count) cu %
        float position = time / SecondsPerKey;
        position %= _keys.Count;
        if (position < 0.0f) position += _keys.Count;   // siguranta pentru time negativ

        // 2. i = cheia de plecare (partea intreaga), u = fractiunea parcursa spre urmatoarea
        int i = (int)MathF.Floor(position);
        float u = position - i;

        // 3. lerp intre cheia i si cheia (i + 1) % Count, cu easing aplicat pe u
        int next = (i + 1) % _keys.Count;
        return Vector3.Lerp(_keys[i], _keys[next], Ease(u));
    }
}
