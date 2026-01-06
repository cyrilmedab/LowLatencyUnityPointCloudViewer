Shader "Custom/PointCloud"
{
    Properties
    {
        _PointSize ("Point Size", Range(0.001, 0.1)) = 0.01
        _MinPointSize ("Min Screen Size (px)", Range(1, 10)) = 2
        _MaxPointSize ("Max Screen Size (px)", Range(1, 50)) = 20
        [Toggle(USE_GEOMETRY_SHADER)] _UseGeometryShader ("Use Geometry Shader (D3D11)", Float) = 0
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
            Name "PointCloudPass"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom

            #pragma multi_compile _ USE_GEOMETRY_SHADER
            #pragma target 4.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PointCloudCommon.hlsl"

            StructuredBuffer<PointData> _PointBuffer;

            // Optional: index buffer for culled rendering
            StructuredBuffer<uint> _IndexBuffer;
            uint _UseIndexBuffer;
            uint _PointCount;

            float _PointSize;
            float _MinPointSize;
            float _MaxPointSize;
            float4x4 _LocalToWorld;

            struct VertexInput
            {
                uint vertexID : SV_VertexID;
            };

            struct VertexOutput
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float pointSize : PSIZE;
                float3 positionWS : TEXCOORD0;
            };

            struct GeomOutput
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            VertexOutput vert(VertexInput input)
            {
                VertexOutput output;

                // Get point index (direct or from index buffer)
                uint pointIndex = input.vertexID;
                if (_UseIndexBuffer > 0)
                {
                    pointIndex = _IndexBuffer[input.vertexID];
                }

                // Bounds check
                if (pointIndex >= _PointCount)
                {
                    output.positionCS = float4(0, 0, -1, 1); // Behind camera
                    output.color = float4(0, 0, 0, 0);
                    output.pointSize = 0;
                    output.positionWS = float3(0, 0, 0);
                    return output;
                }

                // Read point data
                PointData point = _PointBuffer[pointIndex];
                float3 positionOS = float3(point.x, point.y, point.z);

                // Transform to world space
                float3 positionWS = mul(_LocalToWorld, float4(positionOS, 1.0)).xyz;
                output.positionWS = positionWS;

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

            // Geometry shader: expand point to camera-facing quad
            [maxvertexcount(4)]
            void geom(point VertexOutput input[1], inout TriangleStream<GeomOutput> outputStream)
            {
                VertexOutput p = input[0];

                // Skip invalid points
                if (p.color.a < 0.01)
                    return;

                // Calculate quad size in clip space
                float2 size = float2(_PointSize, _PointSize) * 0.5;

                // Get camera right and up in world space
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;

                // Quad corners in world space
                float3 corners[4];
                corners[0] = p.positionWS + (-right - up) * size.x;
                corners[1] = p.positionWS + (right - up) * size.x;
                corners[2] = p.positionWS + (-right + up) * size.x;
                corners[3] = p.positionWS + (right + up) * size.x;

                float2 uvs[4] = { float2(0, 0), float2(1, 0), float2(0, 1), float2(1, 1) };

                // Emit quad vertices
                for (int i = 0; i < 4; i++)
                {
                    GeomOutput output;
                    output.positionCS = TransformWorldToHClip(corners[i]);
                    output.color = p.color;
                    output.uv = uvs[i];
                    outputStream.Append(output);
                }
            }

            // Fragment shader for geometry shader path
            float4 fragGeom(GeomOutput input) : SV_Target
            {
                // Circular point with soft edge
                float2 centered = input.uv * 2.0 - 1.0;
                float dist = length(centered);

                // Discard outside circle
                if (dist > 1.0)
                    discard;

                // Soft edge
                float alpha = 1.0 - smoothstep(0.7, 1.0, dist);

                return float4(input.color.rgb, input.color.a * alpha);
            }

            // Fragment shader for PSIZE path (Metal/OpenGL)
            float4 frag(VertexOutput input) : SV_Target
            {
                #if defined(USE_GEOMETRY_SHADER)
                    // This shouldn't be called when geometry shader is active
                    return input.color;
                #else
                    // Simple point rendering
                    return input.color;
                #endif
            }

            ENDHLSL
        }

        // Fallback pass without geometry shader (for PSIZE-supporting platforms)
        Pass
        {
            Name "PointCloudSimple"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vertSimple
            #pragma fragment fragSimple
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PointCloudCommon.hlsl"

            StructuredBuffer<PointData> _PointBuffer;
            StructuredBuffer<uint> _IndexBuffer;
            uint _UseIndexBuffer;
            uint _PointCount;
            float _PointSize;
            float _MinPointSize;
            float _MaxPointSize;
            float4x4 _LocalToWorld;

            struct VertexOutputSimple
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float pointSize : PSIZE;
            };

            VertexOutputSimple vertSimple(uint vertexID : SV_VertexID)
            {
                VertexOutputSimple output;

                uint pointIndex = _UseIndexBuffer > 0 ? _IndexBuffer[vertexID] : vertexID;

                if (pointIndex >= _PointCount)
                {
                    output.positionCS = float4(0, 0, -1, 1);
                    output.color = float4(0, 0, 0, 0);
                    output.pointSize = 0;
                    return output;
                }

                PointData point = _PointBuffer[pointIndex];
                float3 positionOS = float3(point.x, point.y, point.z);
                float3 positionWS = mul(_LocalToWorld, float4(positionOS, 1.0)).xyz;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = UnpackColor(point.rgba);

                float dist = length(_WorldSpaceCameraPos - positionWS);
                float screenSize = _PointSize / max(dist, 0.001) * _ScreenParams.y;
                output.pointSize = clamp(screenSize, _MinPointSize, _MaxPointSize);

                return output;
            }

            float4 fragSimple(VertexOutputSimple input) : SV_Target
            {
                return input.color;
            }

            ENDHLSL
        }
    }

    // Fallback for older hardware
    FallBack "Universal Render Pipeline/Unlit"
}
