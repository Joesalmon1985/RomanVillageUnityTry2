// Opaque unlit shader: vertex colour x _Color, with fog. Used for glowing fixtures, signs and the
// sky dome. Referenced by Assets/World/Game/Resources/World/WorldUnlit.mat so it ships in builds.
Shader "World/Unlit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        [Toggle] _NoFog ("Disable fog", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _NoFog;

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = i.color;
                if (_NoFog < 0.5) { UNITY_APPLY_FOG(i.fogCoord, c); }
                return c;
            }
            ENDCG
        }
    }
}
