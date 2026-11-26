namespace RideBound.Domain.Commitments;

/// <summary>
/// The dimensions of a rider's commitment ledger. The drop ETA total is charged against the rider's budget B; the
/// two stop switch counts have a hard limit of 0 in the final policies; the pickup ETA total and the vehicle switch
/// count are recorded (the vehicle switch count has a hard limit of 0 in the insertion-only policies).
/// </summary>
public enum CommitmentDimension
{
    PickupEtaTotalMs,
    DropEtaTotalMs,
    VehicleSwitchCount,
    PickupStopSwitchCount,
    DropStopSwitchCount,
}

public static class CommitmentDimensionVocabulary
{
    public static IReadOnlyList<CommitmentDimension> Ordered { get; } =
        Array.AsReadOnly(
        [
            CommitmentDimension.PickupEtaTotalMs,
            CommitmentDimension.DropEtaTotalMs,
            CommitmentDimension.VehicleSwitchCount,
            CommitmentDimension.PickupStopSwitchCount,
            CommitmentDimension.DropStopSwitchCount,
        ]);

    public static string ToProtocolValue(CommitmentDimension dimension) =>
        dimension switch
        {
            CommitmentDimension.PickupEtaTotalMs =>
                "pickup_eta_total_ms",
            CommitmentDimension.DropEtaTotalMs =>
                "drop_eta_total_ms",
            CommitmentDimension.VehicleSwitchCount =>
                "vehicle_switch_count",
            CommitmentDimension.PickupStopSwitchCount =>
                "pickup_stop_switch_count",
            CommitmentDimension.DropStopSwitchCount =>
                "drop_stop_switch_count",
            _ => throw new ArgumentOutOfRangeException(nameof(dimension)),
        };
}
