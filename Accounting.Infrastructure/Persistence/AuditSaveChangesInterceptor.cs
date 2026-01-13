using Accounting.Application.Common.Interfaces;
using Accounting.Domain.Common;
using Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

namespace Accounting.Infrastructure.Persistence.Interceptors;

public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;

    public AuditSaveChangesInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAudit(DbContext? context)
    {
        if (context is null) return;

        var now = DateTime.UtcNow;
        var userId = _currentUserService.UserId;

        // 1. Detect changes
        var entries = context.ChangeTracker.Entries().ToList(); // ToList to avoid concurrent modification if we add audit trails

        foreach (var entry in entries)
        {
            // Skip AuditTrail itself to prevent infinite loops (if strictly checking generic types, but AuditTrail is BaseEntity? No audit for AuditTrail)
            if (entry.Entity is AuditTrail) continue;

            // 2. Handle Timestamps & Soft Delete (Existing Logic)
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is IHasTimestamps t)
                {
                    if (t.CreatedAtUtc == default) t.CreatedAtUtc = now;
                    t.UpdatedAtUtc = null;
                }
                if (entry.Entity is ISoftDeletable sd)
                {
                    sd.IsDeleted = false;
                    sd.DeletedAtUtc = null;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Entity is IHasTimestamps t)
                {
                    t.UpdatedAtUtc = now;
                }
            }
            else if (entry.State == EntityState.Deleted)
            {
                if (entry.Entity is ISoftDeletable sd)
                {
                    // Convert to Modified for Soft Delete
                    entry.State = EntityState.Modified;
                    sd.IsDeleted = true;
                    sd.DeletedAtUtc = now;
                    // Note: This changes state to Modified, so Audit Log logic below needs to handle "SoftDelete" detection
                }
            }

            // 4. Normalize Empty Strings to Null for Nullable Varchar Columns
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                foreach (var prop in entry.Properties)
                {
                    if (prop.Metadata.ClrType == typeof(string) 
                        && prop.Metadata.IsNullable 
                        && prop.CurrentValue is string s 
                        && string.IsNullOrWhiteSpace(s))
                    {
                        prop.CurrentValue = null;
                    }
                }
            }

            // 3. Audit Logging
            // Re-evaluate state because Soft Delete might have changed Deleted -> Modified
            var auditEntry = CreateAuditEntry(entry, userId, now);
            if (auditEntry != null)
            {
               context.Set<AuditTrail>().Add(auditEntry);
            }
        }
    }

    private AuditTrail? CreateAuditEntry(EntityEntry entry, int? userId, DateTime now)
    {
        // Don't audit Detached or Unchanged
        if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
            return null;

        var audit = new AuditTrail
        {
            UserId = userId,
            EntityName = entry.Entity.GetType().Name,
            TimestampUtc = now,
            PrimaryKey = GetPrimaryKey(entry)
        };

        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        var action = entry.State switch
        {
            EntityState.Added => "Insert",
            EntityState.Deleted => "Delete", // Hard Delete
            EntityState.Modified => "Update",
            _ => "Unknown"
        };

        // Detect Soft Delete (State is Modified, but IsDeleted became true)
        if (entry.State == EntityState.Modified && entry.Entity is ISoftDeletable sd && sd.IsDeleted)
        {
             // Check if it was just deleted
             var isDeletedProp = entry.Property(nameof(ISoftDeletable.IsDeleted));
             if (isDeletedProp.IsModified && (bool)isDeletedProp.CurrentValue == true && (bool)isDeletedProp.OriginalValue == false)
             {
                 action = "SoftDelete";
             }
        }

        audit.Action = action;

        foreach (var prop in entry.Properties)
        {
            if (prop.IsTemporary) continue; // Skip temp values
            
            string propertyName = prop.Metadata.Name;
            
            switch (entry.State)
            {
                case EntityState.Added:
                    newValues[propertyName] = prop.CurrentValue;
                    break;
                case EntityState.Deleted:
                    oldValues[propertyName] = prop.OriginalValue;
                    break;
                case EntityState.Modified:
                    if (prop.IsModified)
                    {
                        oldValues[propertyName] = prop.OriginalValue;
                        newValues[propertyName] = prop.CurrentValue;
                    }
                    break;
            }
        }

        // Serialize
        if (oldValues.Count > 0) audit.OldValues = JsonSerializer.Serialize(oldValues);
        if (newValues.Count > 0) audit.NewValues = JsonSerializer.Serialize(newValues);

        return audit;
    }

    private string? GetPrimaryKey(EntityEntry entry)
    {
        // Simple generic PK getter
        try
        {
            var key = entry.Metadata.FindPrimaryKey();
            if (key == null) return null;
            
            var values = new List<object>();
            foreach (var property in key.Properties)
            {
                var val = entry.Property(property.Name).CurrentValue;
                if (val != null) values.Add(val);
            }
            
            return string.Join(",", values);
        }
        catch
        {
            return null;
        }
    }
}
