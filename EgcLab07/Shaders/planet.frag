#version 330 core

// TEMA (varianta C): o planeta procedurala.
//   - terenul: zgomot de valoare in 3D (fbm), esantionat pe directia de pe sfera (vLocalPos normalizat),
//     ca sa nu existe nicio cusatura la poli sau pe meridianul 0 (spre deosebire de un zgomot 2D pe UV);
//   - latitudinea: calotele polare apar unde abs(y) al directiei e mare, indiferent de teren;
//   - norii: alt strat de zgomot 3D, deplasat in timp, suprapus peste culoarea solului;
//   - uSeed: adaugat la coordonatele zgomotului; schimbarea lui (cu tasta G, prin Tween) regenereaza
//     complet continentele, lin, fara nicio discontinuitate vizuala.

in vec3 vColor;
in vec2 vUV;
in vec3 vLocalPos;

uniform bool uShowUV;
uniform float uTime;

uniform vec3 uSeed;
uniform float uScale;
uniform float uCloudScale;
uniform float uCloudSpeed;
uniform float uCloudCoverage;
uniform vec3 uColorOcean;
uniform vec3 uColorLand;
uniform vec3 uColorMountain;
uniform vec3 uColorPolar;
uniform vec3 uCloudColor;

out vec4 FragColor;

float hash3(vec3 p)
{
    p = fract(p * vec3(443.897, 441.423, 437.195));
    p += dot(p, p.yzx + 19.19);
    return fract((p.x + p.y) * p.z);
}

float valueNoise3(vec3 p)
{
    vec3 i = floor(p);
    vec3 f = fract(p);
    vec3 u = f * f * (3.0 - 2.0 * f);

    float a000 = hash3(i + vec3(0.0, 0.0, 0.0));
    float a100 = hash3(i + vec3(1.0, 0.0, 0.0));
    float a010 = hash3(i + vec3(0.0, 1.0, 0.0));
    float a110 = hash3(i + vec3(1.0, 1.0, 0.0));
    float a001 = hash3(i + vec3(0.0, 0.0, 1.0));
    float a101 = hash3(i + vec3(1.0, 0.0, 1.0));
    float a011 = hash3(i + vec3(0.0, 1.0, 1.0));
    float a111 = hash3(i + vec3(1.0, 1.0, 1.0));

    float x00 = mix(a000, a100, u.x);
    float x10 = mix(a010, a110, u.x);
    float x01 = mix(a001, a101, u.x);
    float x11 = mix(a011, a111, u.x);

    float y0 = mix(x00, x10, u.y);
    float y1 = mix(x01, x11, u.y);

    return mix(y0, y1, u.z);
}

float fbm3(vec3 p)
{
    float sum = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 5; i++)
    {
        sum += amplitude * valueNoise3(p);
        p *= 2.0;
        amplitude *= 0.5;
    }
    return sum;
}

void main()
{
    if (uShowUV)
    {
        FragColor = vec4(vUV, 0.0, 1.0);
        return;
    }

    vec3 dir = normalize(vLocalPos);

    float height = fbm3(dir * uScale + uSeed);

    vec3 color = mix(uColorOcean, uColorLand, smoothstep(0.45, 0.55, height));
    color = mix(color, uColorMountain, smoothstep(0.62, 0.75, height));

    float polar = smoothstep(0.55, 0.85, abs(dir.y));
    color = mix(color, uColorPolar, polar);

    float cloud = fbm3(dir * uCloudScale + vec3(uTime * uCloudSpeed, 0.0, 0.0));
    float cloudMask = smoothstep(1.0 - uCloudCoverage, 1.0 - uCloudCoverage + 0.15, cloud);
    color = mix(color, uCloudColor, cloudMask * 0.85);

    FragColor = vec4(color * vColor, 1.0);
}