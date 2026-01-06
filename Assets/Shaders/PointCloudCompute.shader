Shader "Custom/PointCloudCompute"
{
   Properties
    {
        _PointSize ("Point Size", Range(0.001, 0.1)) = 0.01
        _MinPointSize ("Min Screen Size (px)", Range(1, 10)) = 2
        _MaxPointSize ("Max Screen Size (px)", Range(1, 50)) = 20
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "PointCloudComputePass"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PointCloudCommon.hlsl"

            StructuredBuffer<PointData> _PointBuffer;
            uint _PointCount;
            float _PointSize;
            float _MinPointSize;
            float _MaxPointSize;
            float4x4 _LocalToWorld;

            struct VertexInput
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct VertexOutput
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float pointSize : PSIZE;
            };

            VertexOutput vert(VertexInput input)
            {
                VertexOutput output;

                // With DrawProceduralIndirect, instanceID maps to the point index
                uint pointIndex = input.instanceID;

                // Bounds check
                if (pointIndex >= _PointCount)
                {
                    output.positionCS = float4(0, 0, -1, 1); // Behind camera
                    output.color = float4(0, 0, 0, 0);
                    output.pointSize = 0;
                    return output;
                }

                // Read point data
                PointData point = _PointBuffer[pointIndex];
                float3 positionOS = float3(point.x, point.y, point.z);

                // Transform to world space
                float3 positionWS = mul(_LocalToWorld, float4(positionOS, 1.0)).xyz;

                // Transform to clip space
                output.positionCS = TransformWorldToHClip(positionWS);

                // Unpack color
                output.color = UnpackColor(point.rgba);

                // Calculate point size based on distance
                float dist = length(_WorldSpaceCameraPos - positionWS);
                float screenSize = _PointSize / max(dist, 0.001) * _ScreenParams.y;
                output.pointSize = clamp(screenSize, _MinPointSize, _MaxPointSize);

                return output;
            }

            float4 frag(VertexOutput input) : SV_Target
            {
                return input.color;
            }

            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
