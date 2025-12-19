# SpiceSharp.Api.Plot

A plotting library for generating visualizations from SPICE circuit analysis results.

## Overview

This library provides functionality to generate plots (SVG and PNG) from SPICE analysis results, including:
- DC sweep plots
- Transient analysis plots
- AC analysis (Bode plots)
- Operating point bar charts
- Scatter plots

## Features

- Multiple plot types: Line, Bode, Bar, Scatter
- Multiple output formats: SVG, PNG
- Customizable options: colors, scales, grid, legend
- Logarithmic and linear scales
- Support for complex data (magnitude/phase conversion)

## Dependencies

- `OxyPlot.Core` - Core plotting engine
- `OxyPlot.SkiaSharp` - PNG rendering backend
- `Microsoft.Extensions.Logging.Abstractions` - Logging interface

## Usage

```csharp
var generator = PlotGeneratorFactory.Create();
var request = new PlotRequest
{
    AnalysisType = AnalysisType.DcSweep,
    PlotType = PlotType.Line,
    ImageFormat = ImageFormat.Svg,
    // ... configure request
};
var result = generator.GeneratePlot(request);
```

## Development

This project follows Test-Driven Development (TDD) principles. All public APIs must have comprehensive test coverage.

## Status

🚧 In Development - Phase 0: Project Setup

