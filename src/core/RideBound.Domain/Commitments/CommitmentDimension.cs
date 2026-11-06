namespace RideBound.Domain.Commitments;

public enum CommitmentDimension
{
    PickupEtaTotalMs,
    DropEtaTotalMs,
    MaterialEtaRevisionCount,
    VehicleSwitchCount,
    PickupStopRelocationMm,
    PickupStopSwitchCount,
    DropStopRelocationMm,
    DropStopSwitchCount,
    IncumbentOrderInversionCount,
    PrePickupInsertedStopCount,
}

public static class CommitmentDimensionVocabulary
{
    public static IReadOnlyList<CommitmentDimension> Ordered { get; } =
        Array.AsReadOnly(
        [
            CommitmentDimension.PickupEtaTotalMs,
            CommitmentDimension.DropEtaTotalMs,
            CommitmentDimension.MaterialEtaRevisionCount,
            CommitmentDimension.VehicleSwitchCount,
            CommitmentDimension.PickupStopRelocationMm,
            CommitmentDimension.PickupStopSwitchCount,
            CommitmentDimension.DropStopRelocationMm,
            CommitmentDimension.DropStopSwitchCount,
            CommitmentDimension.IncumbentOrderInversionCount,
            CommitmentDimension.PrePickupInsertedStopCount,
        ]);

    public static string ToProtocolValue(CommitmentDimension dimension) =>
        dimension switch
        {
            CommitmentDimension.PickupEtaTotalMs =>
                "pickup_eta_total_ms",
            CommitmentDimension.DropEtaTotalMs =>
                "drop_eta_total_ms",
            CommitmentDimension.MaterialEtaRevisionCount =>
                "material_eta_revision_count",
            CommitmentDimension.VehicleSwitchCount =>
                "vehicle_switch_count",
            CommitmentDimension.PickupStopRelocationMm =>
                "pickup_stop_relocation_mm",
            CommitmentDimension.PickupStopSwitchCount =>
                "pickup_stop_switch_count",
            CommitmentDimension.DropStopRelocationMm =>
                "drop_stop_relocation_mm",
            CommitmentDimension.DropStopSwitchCount =>
                "drop_stop_switch_count",
            CommitmentDimension.IncumbentOrderInversionCount =>
                "incumbent_order_inversion_count",
            CommitmentDimension.PrePickupInsertedStopCount =>
                "pre_pickup_inserted_stop_count",
            _ => throw new ArgumentOutOfRangeException(nameof(dimension)),
        };
}
