using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Runs;

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
    /// Every request that is waiting in both states and changed nothing but its vehicle,
    /// in request-id order. The caller decides how many it accepts; any other change to a
    /// non-pending request is not listed and stays the caller's boundary violation.
    /// </summary>
    public static IReadOnlyList<WaitingRiderRelocation> Find(
        RideBoundRun reduced,
        RideBoundRun candidate)
    {
        ArgumentNullException.ThrowIfNull(reduced);
        ArgumentNullException.ThrowIfNull(candidate);
        List<WaitingRiderRelocation>? found = null;

        foreach (var (requestId, current) in reduced.Requests)
        {
            if (candidate.Requests.TryGetValue(requestId, out var proposed)
                && !ReferenceEquals(current, proposed)
                && FromChange(current, proposed) is { } relocation)
            {
                (found ??= []).Add(relocation);
            }
        }

        return found is null
            ? []
            : found.OrderBy(value => value.RequestId.Value, StringComparer.Ordinal).ToArray();
    }

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
