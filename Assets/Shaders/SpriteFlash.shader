// Sprite shader with a solid-colour flash, for hit feedback.
//
// Blending the sprite toward a flat colour is what reads as "that hit landed" without
// authoring a second set of sprites. The flash amount is driven per-renderer through a
// MaterialPropertyBlock, so a hundred enemies flashing at once still share one material
// and cause no extra draw calls.
Shader "DungeonSong/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0,1)) = 0

        // Kept so the material stays compatible with Unity's sprite tooling.
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment FlashFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            UNITY_INSTANCING_BUFFER_START(FlashProps)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _FlashColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashAmount)
            UNITY_INSTANCING_BUFFER_END(FlashProps)

            fixed4 FlashFrag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;

                fixed4 flashColor = UNITY_ACCESS_INSTANCED_PROP(FlashProps, _FlashColor);
                float flashAmount = UNITY_ACCESS_INSTANCED_PROP(FlashProps, _FlashAmount);

                // Blend toward the flash colour, preserving the sprite's own alpha so the
                // silhouette stays intact rather than becoming a solid rectangle.
                c.rgb = lerp(c.rgb, flashColor.rgb * c.a, flashAmount);

                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
