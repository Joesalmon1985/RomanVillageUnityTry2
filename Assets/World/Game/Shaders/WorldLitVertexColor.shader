// Lambert shader that multiplies _Color by mesh vertex colours (for generated terrain and props).
// Referenced by Assets/World/Game/Resources/World/WorldLitVertexColor.mat so it ships in builds.
Shader "World/LitVertexColor"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _SelfLit ("Night self-light (x albedo)", Range(0,1)) = 0
        _Rim ("Night rim colour", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert fullforwardshadows
        #pragma target 3.0

        fixed4 _Color;
        // Package L (Docs/LIGHTING.md): night readability. _WorldNight is a global (0 day, 1 night) set by DayNight.
        half _SelfLit; fixed4 _Rim; half _WorldNight;

        struct Input
        {
            float4 color : COLOR;
            float3 viewDir;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = IN.color * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = (c.rgb * _SelfLit + _Rim.rgb * rim * rim * rim) * _WorldNight;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
