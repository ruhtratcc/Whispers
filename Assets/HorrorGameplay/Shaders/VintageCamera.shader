Shader "Hidden/WhispersOfPast/VintageCamera"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Analogue image"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            float4 _VintageSignal; // blend, grain, chromatic pixels, softness
            float4 _VintageTextureEffects; // scanlines, tracking pixels
            float _VintageClock;
            float4 _VintageResolution;
            float4 _VintageTape; // school chroma bleed, gap seconds, brief tracking duration, event pulse
            float4 _VintageGrade; // saturation, contrast, soft black lift, palette strength
            float4 _VintageLens; // vignette, explicit horror grade enabled
            float4 _VintageComposite; // enabled, colour trail in 720p pixels, noise raster rows
            float _VintageDarkness; // school-only exhaustion, never applied to overlay UI

            half3 ReadSignal(float2 uv)
            {
                half3 sourceColour = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv)).rgb;
                return _VintageComposite.x > .5 ? LinearToSRGB(max(0, sourceColour)) : sourceColour;
            }
            float3 ToYIQ(float3 c)
            {
                return float3(dot(c, float3(.299,.587,.114)),
                    dot(c, float3(.596,-.274,-.322)), dot(c, float3(.211,-.523,.312)));
            }
            float3 FromYIQ(float3 c)
            {
                return float3(c.x + .956*c.y + .621*c.z,
                    c.x - .272*c.y - .647*c.z, c.x - 1.106*c.y + 1.703*c.z);
            }

            float Noise(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float2 texel = _VintageResolution.xy;
                float2 fromCenter = uv - .5;
                float periphery = smoothstep(.04, .48, length(fromCenter));
                float dread = saturate(_VintageDarkness);
                float breath = .5 + .5 * sin(_VintageClock * 1.8);
                // Slow edge warping grows with exposure; the reticle and printed clues remain steady.
                uv += fromCenter * periphery * dread * (.035 + breath * .14);
                uv.x += sin(uv.y * 13 + _VintageClock * .85) * periphery * dread * dread * .018;
                uv = saturate(uv);
                float2 pixels = uv * _VintageResolution.zw;
                float frame = floor(_VintageClock * lerp(24.0, 29.97, _VintageComposite.x));

                // A shallow, feathered tracking disturbance rolls past every nine seconds.
                float bandDistance = abs(frac(uv.y - _VintageClock / 9.0 + .5) - .5);
                float band = exp(-bandDistance * bandDistance / .00045);
                float tape = step(.01, _VintageTape.y);
                // School tracking is a brief head-switching fault separated by long clean intervals.
                // Unconfigured cameras retain the legacy rolling treatment.
                float gap = max(1, _VintageTape.y);
                float duration = max(.05, _VintageTape.z);
                float phase = fmod(_VintageClock, gap);
                float pulsePhase = saturate((phase - gap + duration) / duration);
                float pulse = step(.001, _VintageTape.z) * step(gap - duration, phase)
                    * sin(pulsePhase * 3.14159265);
                float tapeBand = exp(-pow((uv.y - (.88 - pulsePhase * .64)) / .025, 2));
                band = lerp(band, tapeBand * max(pulse, _VintageTape.w), tape);
                float unstable = sin(uv.y * 170.0 + _VintageClock * 7.2) * .30
                    + sin(_VintageClock * 1.3) * .15;
                float jitter = _VintageTextureEffects.y * band * unstable;
                jitter += tape * _VintageTape.w * sin(pixels.y * .067 + _VintageClock * 13) * 2;
                // Tape timing affects alternating image rows, never the player's camera pose.
                float row = floor(pixels.y);
                jitter += _VintageComposite.x * _VintageTextureEffects.y
                    * (fmod(row, 2) * 2 - 1) * sin(_VintageClock * 13000);
                float stretch = 0;
                if (_VintageComposite.x > .5)
                {
                    float3 centers = frac(_VintageClock * float3(.033, .047, .021) + float3(.17,.54,.83));
                    float3 windows = saturate(1 - abs(uv.y - centers) / (texel.y * float3(10,8,12)));
                    stretch = dot(windows, float3(.7,-.5,.4)) * sin(row*.35 + _VintageClock*3) * texel.y;
                }
                float2 displaced = saturate(uv + float2(texel.x * jitter, stretch));
                half4 center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, displaced);
                if (_VintageComposite.x > .5) center.rgb = LinearToSRGB(max(0, center.rgb));
                float edge = saturate(length(uv - .5) * 1.4);
                float2 fringe = float2(texel.x * (_VintageSignal.z * (.25 + edge)
                    + dread * dread * periphery * 11), 0);
                half3 left = ReadSignal(displaced - fringe);
                half3 right = ReadSignal(displaced + fringe);
                half3 colour = half3(left.r, center.g, right.b);
                colour = lerp(colour, (left + right + center.rgb * 2) * .25, _VintageSignal.w);
                half3 chromaSmear = (left * 2 + right + center.rgb) * .25;
                if (_VintageComposite.x > .5)
                {
                    // Colour has less horizontal bandwidth than detail on a composite signal.
                    // This is a colour-only trail, so doorframes and printed clues stay readable.
                    float2 trail = float2(texel.x * _VintageComposite.y * _VintageResolution.w / 720, 0);
                    float3 a = ToYIQ(ReadSignal(displaced - trail * .20));
                    float3 b = ToYIQ(ReadSignal(displaced - trail * .45));
                    float3 c = ToYIQ(ReadSignal(displaced - trail * .70));
                    float3 d = ToYIQ(ReadSignal(displaced - trail));
                    float3 signal = ToYIQ(center.rgb);
                    float luminanceBand = max(0, signal.x*.70 + a.x*.30 + (signal.x-b.x)*.08);
                    colour = FromYIQ(float3(luminanceBand, signal.y, signal.z));
                    signal.x = luminanceBand;
                    signal.y = signal.y*.35 + a.y*.30 + b.y*.20 + c.y*.10 + d.y*.05;
                    signal.z = signal.z*.15 + a.z*.25 + b.z*.25 + c.z*.20 + d.z*.15;
                    chromaSmear = FromYIQ(signal);
                }
                float smearLuma = dot(chromaSmear, float3(.2126, .7152, .0722));
                float detailLuma = dot(colour, float3(.2126, .7152, .0722));
                // Spread colour horizontally while retaining the luminance detail used to read doorways.
                half3 chroma = chromaSmear - smearLuma;
                float safeChroma = min(1, detailLuma / max(.00001, -min(chroma.r, min(chroma.g, chroma.b))));
                colour = lerp(colour, detailLuma + chroma * safeChroma, _VintageTape.x * tape);
                float2 doubleVision = float2(.025 + .008 * sin(_VintageClock * .9),
                    .009 * sin(_VintageClock * 1.15)) * periphery * dread * dread;
                colour = lerp(colour, (colour + ReadSignal(displaced + doubleVision)) * .5,
                    smoothstep(.35, .9, dread) * periphery * .55);
                float luminance = dot(center.rgb, float3(.2126, .7152, .0722));
                if (_VintageLens.y > .5)
                {
                    // Grade the scene, not the overlay UI. A gentle toe preserves dark doorway detail;
                    // the tiny lifted floor resembles tape blacks without turning the night grey.
                    float gradeLuma = max(0, dot(colour, float3(.2126, .7152, .0722)));
                    colour = lerp(gradeLuma.xxx, colour, saturate(_VintageGrade.x));
                    colour *= 1 + _VintageGrade.y * (gradeLuma - .22) / (gradeLuma + .22);
                    float highlights = saturate((gradeLuma - .32) * 1.6);
                    float3 palette = lerp(float3(.72, .98, 1.10), float3(1.08, 1.015, .86), highlights);
                    colour *= lerp(float3(1, 1, 1), palette, _VintageGrade.w);
                    colour += _VintageGrade.z * (1 - saturate(gradeLuma * 4));
                    float edgeRadius = length((uv - .5) * float2(1.12, 1));
                    colour *= 1 - smoothstep(.26, .72, edgeRadius) * _VintageLens.x;
                }
                // Fine image grain: avoid bright sparkles in total darkness or crushed blacks.
                float2 noisePixels = pixels;
                if (_VintageComposite.x > .5)
                    noisePixels = uv * float2(_VintageComposite.z * _VintageResolution.z / _VintageResolution.w, _VintageComposite.z);
                float grain = (Noise(floor(noisePixels) + float2(frame * 3.1, frame * .71)) - .5)
                    * _VintageSignal.y * lerp(.35 + .65 * sqrt(saturate(luminance)),
                        .06 + .94 * sqrt(saturate(luminance)), _VintageLens.y);
                float scan = sin(lerp(pixels.y * 3.14159265,
                    uv.y * min(380, _VintageResolution.w * .42) * 6.2831853, tape))
                    * _VintageTextureEffects.x * .5;
                colour = max(0, colour * (1 + scan + band * .008) + grain);
                float dreadLuma = dot(colour, float3(.2126, .7152, .0722));
                colour = lerp(colour, dreadLuma.xxx, dread * .38);
                colour *= 1 - periphery * dread * (.48 + breath * .12);
                if (_VintageComposite.x > .5) colour = SRGBToLinear(colour);
                return half4(lerp(original.rgb, colour, saturate(_VintageSignal.x)), original.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
