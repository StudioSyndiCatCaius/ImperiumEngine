using System.Numerics;
using Engine.Globals;
using R3D_cs;
using Raylib_cs;
using Material = R3D_cs.Material;

namespace Engine.Assets.Materials;

public class A_M3_Surface : A_Material
{
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [ImpVar][Category("Wall")] public TMaterialCommons wall_config;
    [ImpVar][Category("Wall")] public float wall_tiling=1;
    
    [ImpVar][Category("Slope")] public bool floor_enabled;
    [ImpVar][Category("Slope")] public Vector3 slope_normal=GMath.WORLD_UP;
    [ImpVar][Category("Slope")] public float slope_offset=0;
    [ImpVar][Category("Slope")] public float slope_sharpness=0;
    
    [ImpVar][Category("Floor")] public TMaterialCommons floor_config;
    [ImpVar][Category("Floor")] public float floor_tiling=1;

    static SurfaceShader _prog;
    SurfaceShader _alias;

    protected override Material Material_Build()
    {
        Material mat = R3D.GetDefaultMaterial();
        wall_config.Apply(ref mat);
        if (_prog.Equals(default(SurfaceShader)))
            _prog = R3D.LoadSurfaceShaderFromMemory(SHADER);
        if (!_prog.Equals(default(SurfaceShader)) && _alias.Equals(default(SurfaceShader)))
            _alias = R3D.LoadSurfaceShaderAlias(_prog);
        if (_alias.Equals(default(SurfaceShader))) return mat;

        float wt = wall_tiling;
        float ft = floor_tiling;
        int floor_on = floor_enabled ? 1 : 0;
        Vector3 sn = slope_normal;
        float off = slope_offset;
        float sharp = slope_sharpness;
        Color tc = floor_config.color_tint;
        Vector4 tint = new(tc.R / 255f, tc.G / 255f, tc.B / 255f, tc.A / 255f);
        Vector4 orm = new(floor_config.occlusion_intensity, floor_config.roughness_intensity, floor_config.metal_intensity, floor_config.specular_intensity);
        float nrm = floor_config.normal_intensity;
        Color ec = floor_config.emission_color;
        Vector4 emis = new(ec.R / 255f, ec.G / 255f, ec.B / 255f, floor_config.emission_intensity);
        R3D.SetSurfaceShaderUniform(_alias, "u_wall_tiling", ref wt);
        R3D.SetSurfaceShaderUniform(_alias, "u_floor_tiling", ref ft);
        R3D.SetSurfaceShaderUniform(_alias, "u_floor_enabled", ref floor_on);
        R3D.SetSurfaceShaderUniform(_alias, "u_slope_normal", ref sn);
        R3D.SetSurfaceShaderUniform(_alias, "u_slope_offset", ref off);
        R3D.SetSurfaceShaderUniform(_alias, "u_slope_sharpness", ref sharp);
        R3D.SetSurfaceShaderUniform(_alias, "u_floor_tint", ref tint);
        R3D.SetSurfaceShaderUniform(_alias, "u_floor_orm_mul", ref orm);
        R3D.SetSurfaceShaderUniform(_alias, "u_floor_normal_scale", ref nrm);
        R3D.SetSurfaceShaderUniform(_alias, "u_floor_emission_mul", ref emis);
        R3D.SetSurfaceShaderSampler(_alias, "u_floor_albedo", Sampler(floor_config.color_map, false));
        R3D.SetSurfaceShaderSampler(_alias, "u_floor_orm", Sampler(floor_config.ORM_map, false));
        R3D.SetSurfaceShaderSampler(_alias, "u_floor_normal", Sampler(floor_config.normal_map, true));
        R3D.SetSurfaceShaderSampler(_alias, "u_floor_emission", Sampler(floor_config.emission_map, false));
        mat.Shader = _alias;
        return mat;
    }

    static Texture2D _white;
    static Texture2D _flat_n;

    static Texture2D Sampler(A_Texture tex, bool normal)
    {
        Texture2D gpu = TMaterialCommons.Gpu(tex);
        if (gpu.Id != 0) return gpu;
        if (normal)
        {
            if (_flat_n.Id == 0)
            {
                Image img = Raylib.GenImageColor(1, 1, new Color(128, 128, 255, 255));
                _flat_n = Raylib.LoadTextureFromImage(img);
                Raylib.UnloadImage(img);
            }
            return _flat_n;
        }
        if (_white.Id == 0)
        {
            Image img = Raylib.GenImageColor(1, 1, Color.White);
            _white = Raylib.LoadTextureFromImage(img);
            Raylib.UnloadImage(img);
            Raylib.SetTextureWrap(_white, TextureWrap.Repeat);
        }
        return _white;
    }

    const string SHADER = """
#pragma usage opaque shadow
#define R3D_NO_AUTO_FETCH

uniform float u_wall_tiling;
uniform float u_floor_tiling;
uniform int u_floor_enabled;
uniform vec3 u_slope_normal;
uniform float u_slope_offset;
uniform float u_slope_sharpness;
uniform vec4 u_floor_tint;
uniform vec4 u_floor_orm_mul;
uniform float u_floor_normal_scale;
uniform vec4 u_floor_emission_mul;
uniform sampler2D u_floor_albedo;
uniform sampler2D u_floor_orm;
uniform sampler2D u_floor_normal;
uniform sampler2D u_floor_emission;

varying vec3 v_world_pos;

vec4 tri(sampler2D tex, vec3 p, vec3 w) {
    return texture(tex, p.zy) * w.x + texture(tex, p.xz) * w.y + texture(tex, p.xy) * w.z;
}

void vertex() {
    v_world_pos = (MATRIX_MODEL * vec4(POSITION, 1.0)).xyz;
}

void fragment() {
    vec3 n = normalize(NORMAL);
    vec3 an = abs(n) + vec3(1e-5);
    vec3 w = an / (an.x + an.y + an.z);
    float wt = max(u_wall_tiling, 1e-4);
    vec3 pw = v_world_pos * wt;
    vec4 alb = SampleAlbedo(pw.zy) * w.x + SampleAlbedo(pw.xz) * w.y + SampleAlbedo(pw.xy) * w.z;
    vec4 orm = SampleOrm(pw.zy) * w.x + SampleOrm(pw.xz) * w.y + SampleOrm(pw.xy) * w.z;
    vec3 nrm = SampleNormal(pw.zy) * w.x + SampleNormal(pw.xz) * w.y + SampleNormal(pw.xy) * w.z;
    vec3 emi = SampleEmission(pw.zy) * w.x + SampleEmission(pw.xz) * w.y + SampleEmission(pw.xy) * w.z;
    if (u_floor_enabled != 0) {
        float ft = max(u_floor_tiling, 1e-4);
        vec3 pf = v_world_pos * ft;
        vec4 falb = tri(u_floor_albedo, pf, w) * u_floor_tint;
        vec4 form = tri(u_floor_orm, pf, w) * u_floor_orm_mul;
        vec3 fnrm = tri(u_floor_normal, pf, w).xyz;
        fnrm = mix(vec3(0.0, 0.0, 1.0), fnrm * 2.0 - 1.0, u_floor_normal_scale);
        vec3 femi = tri(u_floor_emission, pf, w).rgb * u_floor_emission_mul.rgb * u_floor_emission_mul.a;
        vec3 sn = u_slope_normal;
        float sl = length(sn);
        if (sl > 1e-5) sn /= sl;
        float d = dot(n, sn) - u_slope_offset;
        float blend = u_slope_sharpness <= 1e-5 ? (d > 0.0 ? 1.0 : 0.0) : smoothstep(-u_slope_sharpness, u_slope_sharpness, d);
        alb = mix(alb, falb, blend);
        orm = mix(orm, form, blend);
        nrm = mix(nrm, fnrm, blend);
        emi = mix(emi, femi, blend);
    }
    ALBEDO = alb.rgb;
    ALPHA = alb.a;
    OCCLUSION = orm.r;
    ROUGHNESS = orm.g;
    METALNESS = orm.b;
    SPECULAR = orm.a;
    NORMAL_MAP = nrm;
    EMISSION = emi;
}
""";
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATICS
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [Builtin] public static A_M3_Surface PROTO_WHITE = new()
    {
        wall_config = new() { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.White}
    };
    [Builtin] public static A_M3_Surface PROTO_BLACK = new()
    {
        wall_config = new() { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Black}
    };
    [Builtin] public static A_M3_Surface PROTO_GRAY = new()
    {
        wall_config = new() { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Gray}
    };
    [Builtin] public static A_M3_Surface PROTO_RED = new()
    {
        wall_config = new() { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Red}
    };
    [Builtin] public static A_M3_Surface PROTO_GREEN = new()
    {
        wall_config = new() { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Green}
    };
    [Builtin] public static A_M3_Surface PROTO_BLUE = new()
    {
        wall_config = new() { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Blue}
    };
    [Builtin] public static A_M3_Surface PROTO_YELLOW = new()
    {
        wall_config = new() { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Yellow}
    };
    [Builtin] public static A_M3_Surface PROTO_PURPLE = new()
    {
        wall_config = new() { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Purple}
    };
    [Builtin] public static A_M3_Surface PROTO_ORANGE = new()
    {
        wall_config = new() { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Orange}
    };
    
    [Builtin] public static A_M3_Surface PROTO_2SURFACE = new()
    {
        floor_enabled = true,
        floor_config = new() { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.White},
        wall_config = new() { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Red}
    };
}