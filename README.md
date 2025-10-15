# Custom RayTracing experiment on Unity

[![Solar Potition Transition](http://img.youtube.com/vi/this-eiQw4o/hqdefault.jpg)](https://youtube.com/shorts/this-eiQw4o)

## Overview

This repository contains a Unity project demonstrating real-time ray tracing experiments with soft shadows and temporal blending using DirectX Raytracing (DXR).

## Features

- **Ray Traced Soft Shadows**: Realistic soft shadows using DXR with area light simulation
- **Temporal Accumulation**: Exponential Moving Average (EMA) for noise reduction
- **Per-Camera Management**: Independent shadow texture management for multiple cameras
- **Dynamic Resolution**: Automatic handling of camera resolution changes
- **URP Integration**: Seamless integration with Universal Render Pipeline

## Requirements

- Unity 2022.3 or later
- Universal Render Pipeline (URP) 14.0 or later
- DirectX 12 with ray tracing capable GPU
- Windows 10/11

## Installation

Install the package via OpenUPM Scoped Registry:

1. Open Project Settings (Edit > Project Settings)
2. Navigate to Package Manager
3. Add a new Scoped Registry:
   - **Name**: OpenUPM
   - **URL**: `https://package.openupm.com`
   - **Scope**: `jp.nobnak`
4. Open Package Manager (Window > Package Manager)
5. Select "My Registries" and install "Raytracing Experiments"

## Parameters

- **Angular Diameter** (0-90°): Controls the size of the light source and shadow softness
- **Sample Count** (1-64): Number of shadow rays per pixel (higher = smoother but slower)
- **Temporal Blend** (0-1): Temporal accumulation factor (higher = more temporal smoothing)
