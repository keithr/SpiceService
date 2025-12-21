# HTTP Integration Test Plan

## Overview
This document identifies tests that should have HTTP integration versions to test through the actual ASP.NET Core host and HTTP interface. This is critical for catching DI wiring issues, service resolution order problems, and other issues that only appear in real client usage.

## Why HTTP Integration Tests?
1. **DI Container Differences**: Unit tests use `ServiceCollection` directly, but ASP.NET Core uses a different container with different resolution behavior
2. **Service Resolution Order**: Services are resolved during HTTP request processing, which may differ from test scenarios
3. **Real Client Usage**: HTTP tests simulate actual client behavior through the JSON-RPC interface
4. **Startup Sequence**: HTTP tests exercise the full startup sequence from `Program.cs`

## Test Infrastructure
- **SpiceServiceWebApplicationFactory**: WebApplicationFactory for creating test hosts
- **HttpIntegration namespace**: Contains HTTP integration test versions

## Tests That Need HTTP Versions

### High Priority (Critical Workflows)

1. **LibraryServiceDIWiringTests** ✅ DONE
   - **HTTP Version**: `LibraryServiceDIWiringHttpTests`
   - **Reason**: Tests DI wiring that only fails in real usage
   - **Status**: Created

2. **SubcircuitIntegrationTests**
   - **HTTP Version**: `SubcircuitIntegrationHttpTests`
   - **Reason**: Core subcircuit functionality must work through HTTP
   - **Test Cases**:
     - Add subcircuit component via HTTP
     - Import netlist with subcircuits via HTTP
     - Export netlist with subcircuits via HTTP
     - AC/DC/Transient analysis with subcircuits via HTTP

3. **SpeakerSubcircuitAddComponentTests**
   - **HTTP Version**: `SpeakerSubcircuitAddComponentHttpTests`
   - **Reason**: Reproduces exact bug report scenario through HTTP
   - **Test Cases**:
     - Search speakers via HTTP
     - Add speaker subcircuit via HTTP
     - Verify subcircuit works in circuit via HTTP

4. **ImportNetlistSubcircuitBugTests**
   - **HTTP Version**: `ImportNetlistSubcircuitBugHttpTests`
   - **Reason**: Tests subcircuit import bug that may only appear via HTTP
   - **Test Cases**:
     - Import netlist with subcircuits via HTTP
     - Verify subcircuits are registered via HTTP
     - Verify subcircuits work in analysis via HTTP

### Medium Priority (Important Features)

5. **SubcircuitCommonUseCaseTests**
   - **HTTP Version**: `SubcircuitCommonUseCaseHttpTests`
   - **Reason**: Common use cases must work through HTTP
   - **Test Cases**: All common subcircuit workflows via HTTP

6. **LibrarySearchToolTests**
   - **HTTP Version**: `LibrarySearchToolHttpTests`
   - **Reason**: Library search is a core feature
   - **Test Cases**: All library search operations via HTTP

7. **ImportNetlistToolTests**
   - **HTTP Version**: `ImportNetlistToolHttpTests`
   - **Reason**: Netlist import is critical functionality
   - **Test Cases**: All import scenarios via HTTP

8. **PlotResultsSVGValidationTests**
   - **HTTP Version**: `PlotResultsSVGValidationHttpTests`
   - **Reason**: Plot results are returned via HTTP
   - **Test Cases**: All plot result validations via HTTP

### Lower Priority (Nice to Have)

9. **ParameterSweepToolTests**
   - **HTTP Version**: `ParameterSweepToolHttpTests`
   - **Reason**: Parameter sweeps are computationally intensive, good to test via HTTP

10. **TemperatureSweepToolTests**
    - **HTTP Version**: `TemperatureSweepToolHttpTests`
    - **Reason**: Temperature sweeps are computationally intensive, good to test via HTTP

11. **PlotImpedanceToolTests**
    - **HTTP Version**: `PlotImpedanceToolHttpTests`
    - **Reason**: Impedance plots are important for speaker design

12. **OvernightSensationCrossoverTests**
    - **HTTP Version**: `OvernightSensationCrossoverHttpTests`
    - **Reason**: Real-world crossover design workflow

## Test Naming Convention
- HTTP integration tests go in `Services/HttpIntegration/` directory
- Naming: `{OriginalTestName}HttpTests`
- Example: `SubcircuitIntegrationTests` → `SubcircuitIntegrationHttpTests`

## Test Structure Pattern
```csharp
public class {TestName}HttpTests : IClassFixture<SpiceServiceWebApplicationFactory>
{
    private readonly SpiceServiceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public {TestName}HttpTests(SpiceServiceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HttpIntegration_{TestScenario}_ShouldWork()
    {
        // Arrange - Create JSON-RPC request
        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new
            {
                name = "tool_name",
                arguments = new { /* tool args */ }
            }
        };

        // Act - Make HTTP request
        var response = await _client.PostAsJsonAsync("/mcp", request);
        response.EnsureSuccessStatusCode();

        // Assert - Verify response
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        // ... assertions
    }
}
```

## Implementation Status
- [x] Infrastructure (WebApplicationFactory)
- [x] LibraryServiceDIWiringHttpTests
- [ ] SubcircuitIntegrationHttpTests
- [ ] SpeakerSubcircuitAddComponentHttpTests
- [ ] ImportNetlistSubcircuitBugHttpTests
- [ ] Other tests (as needed)

## Notes
- HTTP tests are slower than unit tests, so prioritize critical workflows
- HTTP tests should use `IClassFixture` for shared factory
- Clean up test data (temp files, databases) in `DisposeAsync`
- Use `SpiceServiceWebApplicationFactory.CreateWithTestLibrary()` for tests needing libraries

