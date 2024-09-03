Shader "SimplePlayerCount/SimplePlatformPlayerCountShader"
{
    Properties
    {
        _DigitCount("Digit Count", int) = 2
        [MaterialToggle] _ZeroFill("Zero Fill", float) = 0
        _Color("Color", Color) = (1, 1, 1, 1)
        _BackgroundColor("Background Color", Color) = (0, 0, 0, 1)
        [NoScaleOffset] _HeaderTex ("Header Texture", 2D) = "black" {}
        _HeaderTexSizeX("Header Tex Size X", int) = 64
        _HeaderTexSizeY("Header Tex Size Y", int) = 8
        _HeaderUseSizeX("Header Use Region X", int) = 44
        _HeaderUseSizeY("Header Use Region Y", int) = 8
        [Space]
        [NoScaleOffset] _MainTex ("Texture", 2D) = "black" {}
        _TexSizeX("Tex Size X", int) = 32
        _TexSizeY("Tex Size Y", int) = 32
        _UseSizeX("Use Region X", int) = 31
        _UseSizeY("Use Region Y", int) = 14
        _Col("Column Count", int) = 6
        _Row("Row Count", int) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            #include "../../net.narazaka.vrchat.simple-player-count/Shaders/SimplePlayerCountShader.cginc"
            float _Udon_SimpleUserCountShader_Count_PC;
            float _Udon_SimpleUserCountShader_Count_Mobile;
            sampler2D _HeaderTex;
            float _HeaderTexSizeX;
            float _HeaderTexSizeY;
            float _HeaderUseSizeX;
            float _HeaderUseSizeY;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 colors[3] = {
                    tex2D(_HeaderTex, TiledNumber_focusUV(TiledNumber_placeUV(i.uv, float2(1, 0.5), float2(0, 0.5)), float2(_HeaderUseSizeX / _HeaderTexSizeX, _HeaderUseSizeY / _HeaderTexSizeY), float2(0, 1 - _HeaderUseSizeY / _HeaderTexSizeY))),
                    drawCount(countUV(TiledNumber_placeUV(i.uv, float2(0.5, 0.5), float2(0, 0)), (uint) _Udon_SimpleUserCountShader_Count_PC)),
                    drawCount(countUV(TiledNumber_placeUV(i.uv, float2(0.5, 0.5), float2(0.5, 0)), (uint) _Udon_SimpleUserCountShader_Count_Mobile))
                };
                return colors[(i.uv.y < 0.5) + (i.uv.y < 0.5 && i.uv.x >= 0.5)];
            }
            ENDCG
        }
    }
}
