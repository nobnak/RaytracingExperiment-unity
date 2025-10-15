# Custom RayTracing experiment on Unity

[![Solar Potition Transition](http://img.youtube.com/vi/gkjsNE3j6_Y/hqdefault.jpg)](https://youtube.com/shorts/gkjsNE3j6_Y)

## Overview

This repository contains a Unity project demonstrating real-time ray tracing experiments with soft shadows and temporal blending using DirectX Raytracing (DXR).

## Package: jp.nobnak.raytracing_experiments

The core raytracing implementation is packaged as `jp.nobnak.raytracing_experiments`, located in `Packages/jp.nobnak.raytracing_experiments/`.

### Features

- **Ray Traced Soft Shadows**: Realistic soft shadows using DXR with area light simulation
- **Temporal Accumulation**: Exponential Moving Average (EMA) for noise reduction
- **Per-Camera Management**: Independent shadow texture management for multiple cameras
- **Dynamic Resolution**: Automatic handling of camera resolution changes
- **URP Integration**: Seamless integration with Universal Render Pipeline

### Requirements

- Unity 2022.3 or later
- Universal Render Pipeline (URP) 14.0 or later
- DirectX 12 with ray tracing capable GPU
- Windows 10/11

### Quick Start

1. Open the project in Unity
2. The package is already included in `Packages/jp.nobnak.raytracing_experiments/`
3. Open the sample scene: `Assets/Scenes/SampleScene.unity`
4. Ensure your Graphics API is set to DirectX 12
5. Configure the `RayTracingPassFeature` in your URP Renderer asset

### Package Documentation

For detailed package documentation, see [Packages/jp.nobnak.raytracing_experiments/README.md](Packages/jp.nobnak.raytracing_experiments/README.md)

### Parameters

- **Angular Diameter** (0-90°): Controls the size of the light source and shadow softness
- **Sample Count** (1-64): Number of shadow rays per pixel (higher = smoother but slower)
- **Temporal Blend** (0-1): Temporal accumulation factor (higher = more temporal smoothing)