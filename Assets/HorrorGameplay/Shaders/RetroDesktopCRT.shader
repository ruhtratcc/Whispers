Shader "Whispers/Retro Desktop CRT"
{
    Properties{_MainTex("Desktop",2D)="black"{} _Intensity("CRT intensity",Range(0,1))=.35 _ChromaticPixels("RGB separation (texels)",Range(0,.3))=0 _Curvature("Curvature",Range(0,.004))=.0006 _ColorSaturation("Display saturation",Range(0,1.5))=1.3333 _DisplayGain("Display brightness",Range(.8,1.2))=1.08 _Powered("Powered",Float)=0}
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1"}
        Pass
        {
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)float4 _MainTex_ST,_MainTex_TexelSize;float _Intensity,_Powered,_ChromaticPixels,_Curvature,_ColorSaturation,_DisplayGain;CBUFFER_END
            struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;float2 uv=(p*(1+dot(p,p)*_Curvature*_Intensity)+1)*.5;
                if(any(uv<0)||any(uv>1))return half4(.004,.006,.009,1);
                half3 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb;
                if(_ChromaticPixels>0){float shift=_MainTex_TexelSize.x*_ChromaticPixels*_Intensity;c.r=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+float2(shift,0)).r;c.b=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv-float2(shift,0)).b;}
                float luminance=dot(c,float3(.2126,.7152,.0722));c=saturate(lerp(luminance.xxx,c,_ColorSaturation)*_DisplayGain);
                // Suppress high-frequency scanlines when the physical monitor is smaller than its desktop.
                float footprint=max(length(ddx(uv)*_MainTex_TexelSize.zw),length(ddy(uv)*_MainTex_TexelSize.zw));
                float lines=1-.035*_Intensity*saturate(2-footprint)*(.5+.5*sin(uv.y*_MainTex_TexelSize.w*3.14159265));
                float n=frac(sin(dot(uv*float2(1024,768),float2(12.9898,78.233))+floor(_Time.y*24))*43758.5453)-.5;
                float vignette=1-.06*_Intensity*dot(p,p);float flicker=1-.002*_Intensity*sin(_Time.y*57);
                c=(c*lines*vignette*flicker+n*.003*_Intensity)*lerp(.015,1,saturate(_Powered));
                return half4(max(c,0),1);
            }
            ENDHLSL
        }
    }
}
