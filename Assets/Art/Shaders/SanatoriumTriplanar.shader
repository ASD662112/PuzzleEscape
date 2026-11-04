// 世界坐标三向投影贴图：程序化生成的方块不需要展开 UV，
// 墙面、地板、天花板、门、扶手都能按真实比例平铺真实照片贴图。
// 注意：这里只采样反照率，不做三向法线映射——方块没有切线空间，
// 直接套法线贴图会让整面墙的法线朝错方向、受光变黑。
Shader "PuzzleEscape/WorldTriplanar"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
        _Metallic ("Metallic", Range(0,1)) = 0
        _Scale ("World Scale", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        float _Scale;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = normalize(IN.worldNormal);
            float3 bw = abs(n);
            bw = pow(bw, 4.0);
            bw /= max(bw.x + bw.y + bw.z, 0.0001);

            fixed3 cx = tex2D(_MainTex, IN.worldPos.zy * _Scale).rgb;
            fixed3 cy = tex2D(_MainTex, IN.worldPos.xz * _Scale).rgb;
            fixed3 cz = tex2D(_MainTex, IN.worldPos.xy * _Scale).rgb;

            o.Albedo = (cx * bw.x + cy * bw.y + cz * bw.z) * _Color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
