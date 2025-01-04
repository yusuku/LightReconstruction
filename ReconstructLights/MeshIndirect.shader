Shader "MeshIndirect"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR0;
            };

            StructuredBuffer<float3> _Positions;
            StructuredBuffer<float4> _Colors;

            v2f vert(appdata_base v, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                v2f o;
                uint cmdID = GetCommandID(0);
                uint instanceID = GetIndirectInstanceID(svInstanceID);
                float3 data=_Positions[svInstanceID];
                float4x4 _ObjectToWorld;
                _ObjectToWorld._11_21_31_41 = float4(1, 0, 0, 0);
                _ObjectToWorld._12_22_32_42 = float4(0, 1, 0, 0);
                _ObjectToWorld._13_23_33_43 = float4(0, 0, 1, 0);
                _ObjectToWorld._14_24_34_44 = float4(data.xyz, 1);

                float4 wpos = mul(_ObjectToWorld, v.vertex);
                o.pos = mul(UNITY_MATRIX_VP, wpos);
                o.color = _Colors[svInstanceID];
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }

}
