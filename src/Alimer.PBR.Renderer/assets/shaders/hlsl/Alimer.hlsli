#ifndef _ALIMER_SHADER__
#define _ALIMER_SHADER__

#ifndef SPIRV
#   ifdef __spirv__
#       define SPIRV
#   endif // __spirv__
#endif // SPIRV

#define CONCAT_X(a, b) a##b
#define CONCAT(a, b) CONCAT_X(a, b)

#if defined(SPIRV) || defined(DXIL)
#define CBUFFER(name, slot) ConstantBuffer<type> name : register(b ## slot)
#else
#define CBUFFER(name, slot) cbuffer name : register(b ## slot)
#endif

#define PER_DRAW_CBUFFER_SLOT 0
#define PER_MATERIAL_CBUFFER_SLOT 1
#define PER_VIEW_CBUFFER_SLOT 2
#define PER_FRAME_CBUFFER_SLOT 3

CBUFFER(PerDrawData, PER_DRAW_CBUFFER_SLOT) {
    float4x4 worldMatrix;
};

CBUFFER(PerViewData, PER_VIEW_CBUFFER_SLOT) {
    float4x4 viewMatrix;
    float4x4 projectionMatrix;
    float4x4 viewProjectionMatrix;
    float4x4 inverseProjectionMatrix;
    float4 cameraPosition;
};

static const float PI = 3.141592;
static const float Epsilon = 0.00001;

#endif // _ALIMER_SHADER__
