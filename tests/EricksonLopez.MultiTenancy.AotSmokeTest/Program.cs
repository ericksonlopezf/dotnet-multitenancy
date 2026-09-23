// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics.CodeAnalysis;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Testing;

Console.WriteLine("=================================================");
Console.WriteLine(" EricksonLopez.MultiTenancy NativeAOT Test Suite ");
Console.WriteLine("=================================================");

int passedTests = 0;

void Assert([DoesNotReturnIf(false)] bool condition, string testName)
{
    if (!condition)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {testName}");
        Console.ResetColor();
        Environment.Exit(1);
    }
    passedTests++;
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[PASS] {testName}");
    Console.ResetColor();
}

// ── 1. TenantId Invariants & Value Object Semantics ────────────────────────
Console.WriteLine("\n--- 1. TenantId Invariants ---");

var guid = Guid.NewGuid();
var tenantId1 = TenantId.Create(guid);
var tenantId2 = TenantId.Create(guid);
var tenantId3 = TenantId.Create(Guid.NewGuid());

Assert(tenantId1 == tenantId2, "TenantId equality for same GUID");
Assert(tenantId1 != tenantId3, "TenantId inequality for different GUID");
Assert(!tenantId1.IsEmpty, "TenantId.IsEmpty is false for valid GUID");
Assert(TenantId.Empty.IsEmpty, "TenantId.Empty.IsEmpty is true");
Assert(tenantId1.ToString() == guid.ToString(), "TenantId.ToString() preserves GUID format");

// ── 2. TestTenantContext & Resolution ─────────────────────────────────────
Console.WriteLine("\n--- 2. TestTenantContext & Accessor ---");

var testContext = new TestTenantContext();
Assert(!testContext.IsResolved, "Initial TestTenantContext is unresolved");

var builder = new TenantContextBuilder()
    .WithId(guid)
    .WithName("Acme Corp")
    .WithProperty("Plan", "Enterprise");

var context = builder.BuildContext();
Assert(context.IsResolved, "Built context is resolved");
Assert(context.Tenant!.Name == "Acme Corp", "Tenant name matches builder");
Assert(context.Tenant.Properties["Plan"] == "Enterprise", "Tenant properties match builder");
Assert(context.RequiredTenant.Id == tenantId1, "RequiredTenant returns correct TenantId");

// ── 3. TenantErrors & Results ─────────────────────────────────────────────
Console.WriteLine("\n--- 3. Tenant Errors & Invariants ---");

var notFoundError = TenantErrors.NotFound(tenantId1);
Assert(notFoundError.Code.Length > 0, "TenantErrors.NotFound has code");

var unresolvedError = TenantErrors.Unresolved;
Assert(unresolvedError.Code.Length > 0, "TenantErrors.Unresolved has code");

// ── 4. PlatformAdminContext & ScopedTenantContextAccessor ─────────────────
Console.WriteLine("\n--- 4. PlatformAdminContext & Accessor ---");
var adminCtx = new PlatformAdminContext(isPlatformAdmin: true, auditReason: "AOT-Validation");
Assert(adminCtx.IsPlatformAdmin, "PlatformAdminContext.IsPlatformAdmin is true");
Assert(adminCtx.AuditReason == "AOT-Validation", "PlatformAdminContext.AuditReason matches constructor");

var noneCtx = PlatformAdminContext.None;
Assert(!noneCtx.IsPlatformAdmin, "PlatformAdminContext.None.IsPlatformAdmin is false");

var accessor = new ScopedTenantContextAccessor();
Assert(accessor.TenantContext is null, "Initial ScopedTenantContextAccessor has null context");
accessor.TenantContext = context;
Assert(accessor.TenantContext is not null && accessor.TenantContext.IsResolved, "ScopedTenantContextAccessor holds context");
try
{
    accessor.TenantContext = context;
    Assert(false, "Double assignment should throw InvalidOperationException");
}
catch (InvalidOperationException)
{
    Assert(true, "Double assignment throws InvalidOperationException");
}

Console.WriteLine("\n=================================================");
Console.WriteLine($" ALL {passedTests} NATIVE AOT SUITE TESTS PASSED SUCCESSFULLY! ");
Console.WriteLine("=== AOT Validator: OK ===");
Console.WriteLine("=================================================");
