Shader "Custom/UISpotlightMask"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Overlay Color", Color) = (0, 0, 0, 0.75)
        _SpotlightCenter ("Spotlight Center (Local Space)", Vector) = (0, 0, 0, 0) // x, y = local pos, z = radius, w = feather
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane" 
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color    : COLOR;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color    : COLOR;
                float2 localPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _SpotlightCenter;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;
                o.localPos = v.vertex.xy; // Pass UI local coordinates
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Local coordinate of the fragment
                float2 pos = i.localPos;
                
                // Distance to spotlight center in local units
                float dist = distance(pos, _SpotlightCenter.xy);
                
                float innerRadius = _SpotlightCenter.z;
                float outerRadius = innerRadius + _SpotlightCenter.w;
                
                // Smooth step interpolation
                float t = smoothstep(innerRadius, outerRadius, dist);
                
                fixed4 col = _Color * i.color;
                col.a *= t;
                
                return col;
            }
            ENDCG
        }
    }
}
