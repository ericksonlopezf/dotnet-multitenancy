# Level 08: OpenTelemetry Tracing & Testing Test Doubles

Learn how to observe and test multi-tenant applications effectively.

## Distributed Tracing with `EricksonLopez.MultiTenancy.OpenTelemetry`

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("EricksonLopez.MultiTenancy")
        .AddConsoleExporter());
```

Automatically tags every `Activity` with `tenant.id` and `tenant.name` attributes and propagates context via W3C Baggage headers!

## Unit & Integration Testing with `EricksonLopez.MultiTenancy.Testing`

```csharp
[Fact]
public async Task OrderService_Processes_For_Correct_Tenant()
{
    // Arrange: Build test tenant context using fluent builder
    var context = new TenantContextBuilder()
        .WithId(Guid.NewGuid())
        .WithName("Test Corp")
        .BuildContext();

    var fakeStore = new FakeTenantStore(context.Tenant!);
    var service = new OrderService(context, fakeStore);

    // Act
    var result = await service.GetOrdersAsync();

    // Assert
    result.IsSuccess.Should().BeTrue();
}
```
