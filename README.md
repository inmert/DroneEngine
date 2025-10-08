# Velocity Labs Manta Drone Platform

<div align="center">
  
 ![Manta Banner](https://cdn.sanity.io/images/fuvbjjlp/production/04dc6a81d4228b64d99b9f8f8f42ac16db790c75-1920x1080.jpg)
  
  [![Unity Version](https://img.shields.io/badge/Unity-6.0%2B-black?style=for-the-badge&logo=unity)](https://unity.com/)
  [![License](https://img.shields.io/badge/License-MIT-blue?style=for-the-badge)](LICENSE)
  [![Build Status](https://img.shields.io/badge/Build-Passing-success?style=for-the-badge)](https://github.com/velocitylabs/manta)
  [![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux-lightgrey?style=for-the-badge)](https://github.com/velocitylabs/manta)
  
  **Advanced drone simulation and control platform with integrated sensor suite for industrial inspection and anomaly detection**
  
  [Features](#-features) • [Installation](#-installation) • [Documentation](#-documentation) • [Sensors](#-sensor-suite) • [Contributing](#-contributing)
  
</div>

---

## Overview

The **Manta Drone Platform** is a comprehensive Unity-based flight controller and simulation system designed for industrial inspection, surface defect detection, and anomaly analysis. Built by Velocity Labs, Manta integrates cutting-edge sensor technology with intuitive flight control interfaces.

### Key Highlights

- **Full Flight Controller** - Complete flight control system with real-time telemetry
- **Advanced Sensor Suite** - RealSense D455, LiDAR, Thermal, IR, and Blackfly cameras
- **Defect Detection** - Specialized algorithms for surface inspection and anomaly analysis
- **Modern UI** - Minimal, aerospace-grade interface design
- **Real-time Performance** - Optimized for industrial applications

---

## Features

### Flight Control System
- **Autonomous Flight** - GPS waypoint navigation and path planning
- **Manual Control** - Full 6-DOF manual control with customizable input mapping
- **Stabilization** - Advanced PID control loops for stable flight
- **Telemetry** - Real-time altitude, speed, heading, attitude monitoring
- **Safety Systems** - Geofencing, return-to-home, low battery protection

### Sensor Integration
- **Intel RealSense D455** - Depth sensing and 3D mapping
- **LiDAR** - High-precision distance measurement and terrain mapping
- **Thermal Imaging** - Temperature analysis and heat signature detection
- **Infrared (IR)** - Night vision and material analysis
- **FLIR Blackfly Cameras** - High-resolution surface defect detection

### Analysis Capabilities
- Surface defect detection and classification
- Anomaly detection using computer vision
- Real-time image processing pipelines
- Data logging and export functionality
- 3D reconstruction and mesh generation

---

## Installation

### Prerequisites

```bash
- Unity 6.0 or newer
- .NET Framework 4.8+
- Windows 10/11 or Ubuntu 20.04+
- 8GB RAM minimum (16GB recommended)
- DirectX 11 or Vulkan compatible GPU
```

### Quick Start

1. **Clone the repository**
   ```bash
   git clone https://github.com/velocitylabs/manta.git
   cd manta
   ```

2. **Open in Unity Hub**
   - Add the project folder to Unity Hub
   - Open with Unity 6.0 or newer

3. **Install dependencies**
   - The required packages will auto-import via Unity Package Manager
   - RealSense SDK components included in `/Plugins`

4. **Run the application**
   - Open the `DroneEngine` scene
   - Press Play to launch the flight controller

---

## Sensor Suite

<table>
  <tr>
    <th>Sensor</th>
    <th>Model</th>
    <th>Purpose</th>
    <th>Resolution/Range</th>
  </tr>
  <tr>
    <td>Depth Camera</td>
    <td>Intel RealSense D455</td>
    <td>3D depth mapping, obstacle avoidance</td>
    <td>1280×720 @ 90fps, 6m range</td>
  </tr>
  <tr>
    <td>LiDAR</td>
    <td>Custom Integration</td>
    <td>Precision distance measurement</td>
    <td>100m range, 360° coverage</td>
  </tr>
  <tr>
    <td>Thermal</td>
    <td>Thermal Imaging Suite</td>
    <td>Heat signature analysis</td>
    <td>640×512, -40°C to 550°C</td>
  </tr>
  <tr>
    <td>Infrared</td>
    <td>IR Camera System</td>
    <td>Night vision, material detection</td>
    <td>1920×1080 @ 60fps</td>
  </tr>
  <tr>
    <td>Machine Vision</td>
    <td>FLIR Blackfly S</td>
    <td>Surface defect detection</td>
    <td>5MP, 75fps, USB3 Vision</td>
  </tr>
</table>

---

## Usage

### Launching the Flight Controller

```csharp
// Initialize the drone engine
DroneEngine.Initialize();

// Start flight controller UI
FlightControllerUI.Show();

// Access sensor data
var depthData = RealSenseManager.GetDepthFrame();
var thermalData = ThermalSensor.GetFrame();
```

### Basic Flight Commands

```csharp
// Takeoff
DroneController.Takeoff(altitude: 10f);

// Navigate to waypoint
DroneController.GoToPosition(new Vector3(100, 50, 200));

// Enable autonomous inspection mode
InspectionMode.Start(inspectionPath);

// Land
DroneController.Land();
```

### Defect Detection

```csharp
// Configure detection pipeline
DefectDetector detector = new DefectDetector();
detector.SetSensitivity(0.85f);

// Process frame
var result = detector.AnalyzeFrame(blackflyFrame);

// Get anomalies
foreach (var anomaly in result.Anomalies) {
    Debug.Log($"Defect found at {anomaly.Position}");
}
```

---

## Documentation

- **[Flight Controller Guide](docs/flight-controller.md)** - Complete flight control documentation
- **[Sensor Integration](docs/sensors.md)** - Sensor setup and calibration
- **[API Reference](docs/api.md)** - Full API documentation
- **[Defect Detection](docs/defect-detection.md)** - Surface inspection algorithms
- **[Examples](examples/)** - Sample scripts and use cases

---

## Project Structure

```
manta/
├── Assets/
│   ├── DroneEngine/          # Core flight controller
│   ├── Sensors/              # Sensor models and interfaces
│   │   ├── RealSense/
│   │   ├── LiDAR/
│   │   ├── Thermal/
│   │   ├── IR/
│   │   └── Blackfly/
│   ├── UI/                   # Flight controller UI
│   ├── Scripts/              # C# flight logic
│   └── Resources/            # Assets and models
├── Docs/                     # Documentation
├── Examples/                 # Example scenes
└── Plugins/                  # Native plugins
```

---

## Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## Acknowledgments

- **Intel RealSense** - Depth sensing technology
- **FLIR Systems** - Blackfly camera integration
- **Unity Technologies** - Game engine and UI Toolkit
- **Velocity Labs Team** - Development and testing

---

## Contact

**Velocity Labs**  
Website: [www.velocitylabs.space](https://velocitylabs.space)  
Email: priyan@velocitylabs.space 

---

<div align="center">
  
  **Star us on GitHub — it motivates us a lot!**
  
  Made with ❤️ by Velocity Labs
  
</div>
