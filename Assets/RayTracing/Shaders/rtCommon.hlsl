#ifndef RTCOMMON_HLSL
#define RTCOMMON_HLSL

#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };

CBUFFER_START(CameraBuffer)
int _InstBVHAddr;
int _BVHNodesNum;
int _FramesNum;
float4x4 _InvCameraViewProj;
float4x4 _RasterToCamera;
float4x4 _CameraToWorld;
float4x4 _WorldToRaster;
float3   _CameraPosWS;
float    _CameraFarDistance;
float    _LensRadius;
float    _FocalLength;
float _CameraConeSpreadAngle;
int _FrameIndex;
int _MinDepth;
int _MaxDepth;
int _LightsNum;
int _DebugView;
int _EnvironmentMapEnable;
float _EnvironmentLightPmf;
float4 _ScreenSize;
CBUFFER_END

#endif
