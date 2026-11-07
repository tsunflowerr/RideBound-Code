using RideBound.Domain.Common;
using RideBound.Domain.Requests;

namespace RideBound.Domain.Validation;

/// <summary>
/// One confirmed waiting rider moved from <see cref="DonorVehicleId"/> to
/// <see cref="ReceiverVehicleId"/> by the decision being validated (G2). Validators that
/// check a candidate against the reduced state, where the rider still belongs to the donor,
/// take it as the only reassignment they allow.
/// </summary>
public sealed record WaitingRiderRelocation(
    RequestId RequestId,
    VehicleId DonorVehicleId,
    VehicleId ReceiverVehicleId)
{
    /// <summary>
    /// The relocation that turns <paramref name="current"/> into <paramref name="proposed"/>:
    /// both waiting, on different vehicles, and equal in everything else. Null otherwise.
    /// </summary>
    public static WaitingRiderRelocation? FromChange(RideRequest current, RideRequest proposed)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(proposed);

        return current.Lifecycle == RequestLifecycle.WaitingPickup
            && current.AssignedVehicleId is VehicleId donor
            && proposed.AssignedVehicleId is VehicleId receiver
            && donor != receiver
            && current.ReassignWaiting(receiver).Value == proposed
                ? new WaitingRiderRelocation(current.Id, donor, receiver)
                : null;
    }
}
