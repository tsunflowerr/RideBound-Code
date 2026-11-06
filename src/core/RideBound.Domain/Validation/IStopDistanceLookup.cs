using RideBound.Domain.Common;

namespace RideBound.Domain.Validation;

public interface IStopDistanceLookup
{
    bool TryGetDistanceMillimeters(
        NodeId fromNodeId,
        NodeId toNodeId,
        out long distanceMillimeters);
}
