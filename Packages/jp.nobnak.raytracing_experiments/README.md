# Raytracing Experiments

Unity raytracing experiments with soft shadows and temporal blending.

## Features

- Ray traced soft shadows using DXR
- Temporal accumulation with exponential moving average (EMA)
- Per-camera shadow texture management
- Resolution change detection and automatic resource reallocation
- Soft shadow area light simulation with configurable angular diameter

## Requirements

- Unity 2022.3 or later
- Universal Render Pipeline (URP) 14.0 or later
- DirectX 12 with ray tracing support
- Graphics API: DX12

## Installation

Add this package to your Unity project via Package Manager:

1. Open Package Manager (Window > Package Manager)
2. Click "+" button and select "Add package from disk..."
3. Navigate to and select `package.json` in this directory

Or add to your `manifest.json`:

```json
{
  "dependencies": {
    "jp.nobnak.raytracing_experiments": "file:../Packages/jp.nobnak.raytracing_experiments"
  }
}
```

## Usage

1. Add `RayTracingPassFeature` to your URP Renderer asset
2. Assign the raytracing shader and composite shaders in the feature settings
3. Configure parameters:
   - **Angular Diameter**: Size of the light source in degrees (affects shadow softness)
   - **Sample Count**: Number of shadow rays per pixel
   - **Temporal Blend**: Blend factor for temporal accumulation (higher = more smoothing)

## Components

### RayTracingPassFeature

The main render feature that integrates raytracing into the URP render pipeline.

- Manages per-camera shadow textures
- Handles temporal blending for noise reduction
- Automatically detects and handles resolution changes

### CameraMatrixViewer

Debug utility to display camera matrices in the inspector.

## Shaders

- `RayTracingShader.raytrace`: Main ray tracing shader with soft shadows
- `CompositeMultiply.shader`: Composites shadows with scene color
- `TemporalBlend.shader`: Temporal accumulation shader
- `HitShader.shader`: Ray hit shader for shadow calculations

## License

See LICENSE file for details.

## Author

nobnak

