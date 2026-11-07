"""Register only the RideBound FleetControl; no native FleetPy optimizer."""


def add_fleet_control_modules():
    return {
        "RideBoundFleetControl": (
            "ridebound_fleetpy.fleet_control",
            "RideBoundFleetControl",
        )
    }


def add_broker_modules():
    return {}


def add_charging_strategy_modules():
    return {}


def add_dev_routing_engines():
    return {}


def add_dev_simulation_environments():
    return {}


def add_dynamic_fleetsizing_strategy_modules():
    return {}


def add_dynamic_pricing_strategy_modules():
    return {}


def add_forecast_models():
    return {}


def add_repositioning_modules():
    return {}


def add_request_models():
    return {}


def add_reservation_strategy_modules():
    return {}


def add_ride_pooling_batch_optimizer_modules():
    return {}
