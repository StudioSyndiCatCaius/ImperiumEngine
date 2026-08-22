using System.Numerics;
using R3D_cs;
using Raylib_cs;
using Material = R3D_cs.Material;

namespace ImperiumEngine.Assets.Materials;

public class Mat_Surface : A_Material
{
    [ImpVar][Category("Wall")] public TMaterialCommons wall = new();
    [ImpVar][Category("Wall")] public float wall_tiling=1.0f;

    [ImpVar][Category("Slope")] public Vector3 slope_normal=new(0,1,0);
    [ImpVar][Category("Slope")] public float slope_offset=0.5f;
    [ImpVar][Category("Slope")] public float slope_sharpness=8.0f;

    [ImpVar][Category("Floor")] public bool use_floor;
    [ImpVar][Category("Floor")] public TMaterialCommons floor = new();
    [ImpVar][Category("Floor")] public float floor_tiling=1.0f;

    static SurfaceShader _program;
    static bool _program_ok;
    SurfaceShader _alias;
    bool _alias_ok;

    protected override Material Material_Build()
    {
        Material mat = wall.ToMaterial();
        SurfaceShader sh = Alias_Get();
        if (!_alias_ok)
        {
            return mat;
        }

        mat.Shader = sh;

        float wt = wall_tiling;
        float ft = floor_tiling;
        Vector3 sn = slope_normal;
        float use = 0.0f;
        if (use_floor)
        {
            use = 1.0f;
        }
        Vector3 slope = new Vector3(slope_offset, slope_sharpness, use);
        Vector4 floor_tint = Color_Vec(floor.color_tint);
        Vector4 floor_orm = new Vector4(
            floor.occlusion_intensity,
            floor.roughness_intensity,
            floor.metal_intensity,
            floor.specular_intensity);
        float floor_nscale = floor.normal_intensity;
        Vector4 floor_emi = Color_Vec(floor.emission_color);
        floor_emi.W = floor.emission_intensity;

        R3D.SetSurfaceShaderUniform(sh, "u_wall_tiling", ref wt);
        R3D.SetSurfaceShaderUniform(sh, "u_floor_tiling", ref ft);
        R3D.SetSurfaceShaderUniform(sh, "u_slope_normal", ref sn);
        R3D.SetSurfaceShaderUniform(sh, "u_slope", ref slope);
        R3D.SetSurfaceShaderUniform(sh, "u_floor_tint", ref floor_tint);
        R3D.SetSurfaceShaderUniform(sh, "u_floor_orm", ref floor_orm);
        R3D.SetSurfaceShaderUniform(sh, "u_floor_nscale", ref floor_nscale);
        R3D.SetSurfaceShaderUniform(sh, "u_floor_emi", ref floor_emi);

        Texture2D white = R3D.GetWhiteTexture();
        Texture2D nrm = R3D.GetNormalTexture();
        R3D.SetSurfaceShaderSampler(sh, "u_floor_albedo", TMaterialCommons.Map_Gpu(floor.color_map, white));
        R3D.SetSurfaceShaderSampler(sh, "u_floor_orm_map", TMaterialCommons.Map_Gpu(floor.ORM_map, white));
        R3D.SetSurfaceShaderSampler(sh, "u_floor_normal", TMaterialCommons.Map_Gpu(floor.normal_map, nrm));
        R3D.SetSurfaceShaderSampler(sh, "u_floor_emission", TMaterialCommons.Map_Gpu(floor.emission_map, white));
        return mat;
    }

    SurfaceShader Alias_Get()
    {
        if (_alias_ok)
        {
            return _alias;
        }
        if (!_program_ok)
        {
            Texture2D probe = R3D.GetWhiteTexture();
            if (probe.Id == 0)
            {
                return default;
            }
            _program = R3D.LoadSurfaceShaderFromMemory(SHADER);
            _program_ok = true;
        }
        if (_program.Equals(default(SurfaceShader)))
        {
            return default;
        }
        _alias = R3D.LoadSurfaceShaderAlias(_program);
        if (_alias.Equals(default(SurfaceShader)))
        {
            return default;
        }
        _alias_ok = true;
        return _alias;
    }

    static Vector4 Color_Vec(Color c)
    {
        return new Vector4(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f);
    }

    const string SHADER = """
#pragma usage opaque shadow
#define R3D_NO_AUTO_FETCH

uniform float u_wall_tiling;
uniform float u_floor_tiling;
uniform vec3 u_slope_normal;
uniform vec3 u_slope;
uniform vec4 u_floor_tint;
uniform vec4 u_floor_orm;
uniform float u_floor_nscale;
uniform vec4 u_floor_emi;

uniform sampler2D u_floor_albedo;
uniform sampler2D u_floor_orm_map;
uniform sampler2D u_floor_normal;
uniform sampler2D u_floor_emission;

vec3 tri_w(vec3 n)
{
    vec3 w = pow(max(abs(n), vec3(1e-4)), vec3(4.0));
    return w / (w.x + w.y + w.z);
}

void fragment()
{
    vec3 wp = POSITION;
    vec3 wn = normalize(NORMAL);
    vec3 tw = tri_w(wn);
    float wt = max(u_wall_tiling, 0.001);

    vec2 uvx = wp.zy * wt;
    vec2 uvy = wp.xz * wt;
    vec2 uvz = wp.xy * wt;

    vec4 alb = SampleAlbedo(uvx) * tw.x + SampleAlbedo(uvy) * tw.y + SampleAlbedo(uvz) * tw.z;
    vec4 orm = SampleOrm(uvx) * tw.x + SampleOrm(uvy) * tw.y + SampleOrm(uvz) * tw.z;
    vec3 emi = SampleEmission(uvx) * tw.x + SampleEmission(uvy) * tw.y + SampleEmission(uvz) * tw.z;
    vec3 nrm = SampleNormal(uvx) * tw.x + SampleNormal(uvy) * tw.y + SampleNormal(uvz) * tw.z;

    ALBEDO = alb.rgb;
    ALPHA = alb.a;
    OCCLUSION = orm.r;
    ROUGHNESS = orm.g;
    METALNESS = orm.b;
    SPECULAR = orm.a;
    EMISSION = emi;
    NORMAL_MAP = nrm;

    if (u_slope.z > 0.5)
    {
        float ft = max(u_floor_tiling, 0.001);
        vec2 uvf = wp.xz * ft;
        vec4 falb = texture(u_floor_albedo, uvf) * u_floor_tint;
        vec4 form = texture(u_floor_orm_map, uvf);
        form *= u_floor_orm;
        vec3 femi = texture(u_floor_emission, uvf).rgb * u_floor_emi.rgb * u_floor_emi.a;
        vec3 fnrm = mix(vec3(0.5, 0.5, 1.0), texture(u_floor_normal, uvf).xyz, u_floor_nscale);

        vec3 sn = u_slope_normal;
        float sl = length(sn);
        if (sl < 1e-5)
        {
            sn = vec3(0.0, 1.0, 0.0);
        }
        else
        {
            sn /= sl;
        }
        float t = clamp((dot(wn, sn) - u_slope.x) * max(u_slope.y, 0.0), 0.0, 1.0);

        ALBEDO = mix(ALBEDO, falb.rgb, t);
        ALPHA = mix(ALPHA, falb.a, t);
        OCCLUSION = mix(OCCLUSION, form.r, t);
        ROUGHNESS = mix(ROUGHNESS, form.g, t);
        METALNESS = mix(METALNESS, form.b, t);
        SPECULAR = mix(SPECULAR, form.a, t);
        EMISSION = mix(EMISSION, femi, t);
        NORMAL_MAP = mix(NORMAL_MAP, fnrm, t);
    }
}
""";

    public static Mat_Surface PROTO_TILE_WHITE=new()
    {
        filepath = BuiltinPrefix + "Mat_Surface.PROTO_TILE_WHITE",
        wall = { color_map = A_Texture.S_PROTO_FLOOR }
    };
    public static Mat_Surface PROTO_TILE_RED=new()
    {
        filepath = BuiltinPrefix + "Mat_Surface.PROTO_TILE_RED",
        wall = { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Red }
    };
    public static Mat_Surface PROTO_TILE_BLUE=new()
    {
        filepath = BuiltinPrefix + "Mat_Surface.PROTO_TILE_BLUE",
        wall = { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Blue }
    };
    public static Mat_Surface PROTO_TILE_GREEN=new()
    {
        filepath = BuiltinPrefix + "Mat_Surface.PROTO_TILE_GREEN",
        wall = { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Green }
    };
    public static Mat_Surface PROTO_TILE_PURPLE=new()
    {
        filepath = BuiltinPrefix + "Mat_Surface.PROTO_TILE_PURPLE",
        wall = { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Purple }
    };
    public static Mat_Surface PROTO_TILE_ORANGE=new()
    {
        filepath = BuiltinPrefix + "Mat_Surface.PROTO_TILE_ORANGE",
        wall = { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Orange }
    };
    public static Mat_Surface PROTO_TILE_YELLOW=new()
    {
        filepath = BuiltinPrefix + "Mat_Surface.PROTO_TILE_YELLOW",
        wall = { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Yellow }
    };

    public static Mat_Surface PROTO_TILE_2SURFACE=new()
    {
        filepath = BuiltinPrefix + "Mat_Surface.PROTO_TILE_2SURFACE",
        use_floor = true,
        slope_offset = 0.5f,
        slope_sharpness = 8.0f,
        floor = { color_map = A_Texture.S_PROTO_FLOOR },
        wall = { color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Red }
    };
}
