namespace QueryDuck.Sample.Entities;

/// <summary>
/// Customer entity shaped for Oracle debugging scenarios.
/// Region is mapped as non-nullable but the database column allows NULL (Phase 4 audit target).
/// </summary>
public sealed class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Intentionally non-nullable in the model; Oracle column is nullable.
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Used to demonstrate Oracle empty-string-is-NULL behavior (Phase 2 QD001).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<Order> Orders { get; } = [];
}
