// Lambert shader that multiplies _Color by mesh vertex colours (for generated terrain and props).
// Referenced by Assets/World/Game/Resources/World/WorldLitVertexColor.mat so it ships in builds.
Shader "World/LitVertexColor"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _SelfLit ("Night self-light (x albedo)", Range(0,1)) = 0
        _Rim ("Night rim colour", Color) = (0,0,0,0)
        _Dissolve ("Dissolve (WORLD_DISSOLVE only)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert fullforwardshadows addshadow
        #pragma target 3.0
        // P3: a dissolve for the fallen (soldiers): only materials with WORLD_DISSOLVE pay for the clip.
        #pragma multi_compile_local __ WORLD_DISSOLVE

        fixed4 _Color;
        // Package L (Docs/LIGHTING.md): night readability. _WorldNight is a global (0 day, 1 night) set by DayNight.
        half _SelfLit; fixed4 _Rim; half _WorldNight;
        half _Dissolve;

        struct Input
        {
            float4 color : COLOR;
            float3 viewDir;
            float3 worldPos; // P3 dissolve
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = IN.color * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = (c.rgb * _SelfLit + _Rim.rgb * rim * rim * rim) * _WorldNight;
        #ifdef WORLD_DISSOLVE
            // Blocky noise in the part's own space (8 cells per unit, so any scale dissolves alike): cells vanish as
            // _Dissolve rises, with a dark ashen rim at the edge.
            float3 cell = floor(mul(unity_WorldToObject, float4(IN.worldPos, 1.0)).xyz * 8.0);
            float n = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
            clip(n - _Dissolve);
            o.Albedo *= lerp(1.0, 0.35, saturate((_Dissolve + 0.12 - n) / 0.12) * step(0.001, _Dissolve));
        #endif
        }
        ENDCG
    }
    FallBack "Diffuse"
}
