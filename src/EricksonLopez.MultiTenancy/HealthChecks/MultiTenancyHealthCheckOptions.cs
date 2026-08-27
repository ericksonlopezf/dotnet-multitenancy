// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EricksonLopez.MultiTenancy.HealthChecks;

/// <summary>
/// Provides configuration options for <see cref="MultiTenancyHealthCheck"/>.
/// </summary>
public sealed class MultiTenancyHealthCheckOptions
{
    /// <summary>
    /// Gets or sets the health status returned when no <see cref="ITenantStore"/> is registered.
    /// </summary>
    public HealthStatus FailureStatus { get; set; } = HealthStatus.Degraded;

    /// <summary>
    /// Gets or sets a value indicating whether diagnostic metadata about the store is included in the health check result data.
    /// </summary>
    public bool IncludeDiagnosticData { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional asynchronous probe delegate used to verify tenant store responsiveness.
    /// </summary>
    public Func<ITenantStore, CancellationToken, Task<bool>>? StoreProbe { get; set; }
}
