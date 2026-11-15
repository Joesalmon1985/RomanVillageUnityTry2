// Draws nothing: the material of character pieces a costume hides (Role_hidden, PRD_CHARACTERS 5.6). The WP-6
// builder drops hidden pieces from the merged mesh, so this only stands in for the FBX's own Role_hidden material.
Shader "World/ActorHidden"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            ColorMask 0
            ZWrite Off
            ZTest Never
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 vert (float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }
            fixed4 frag () : SV_Target { return 0; }
            ENDCG
        }
    }
}
