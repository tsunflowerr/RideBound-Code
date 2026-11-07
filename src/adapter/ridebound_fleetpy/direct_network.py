"""ADR-089 (GĐ3): FleetPy NetworkBasic on a complete metric graph, routed along the direct edge.

The full-node Via cells (824-858 nodes, about 680,000 directed arcs) are complete graphs whose arc times are an integer
Floyd-Warshall closure, so the direct edge o->d is a shortest path. NetworkBasic finds it with a Python Dijkstra over
every edge, which is far too slow at that size, and where a two-arc path ties with the direct arc (the closure makes
many such ties) floating-point rounding decides which one it returns. The emulator moves a vehicle directly from node
to node (engine17.js advance()).

CompleteGraphDirectNetwork keeps NetworkBasic whole (nodes, edges, travel-time updates, vehicle movement) and reuses
its routing methods unchanged except for the single node-to-node search: each of them is rebound to the same code with
`Router` replaced by DirectRouter, which answers with the direct edge. Trivial routes, edge overheads, time radius and
target limits are therefore handled by NetworkBasic's own code. A customized section cost function falls back to the
real Router (the RideBound adapter never passes one).
"""

from __future__ import annotations

import types

from src.routing.NetworkBasic import NetworkBasic
from src.routing.routing_imports.Router import Router

INFINITE = (float("inf"), float("inf"), float("inf"))


class DirectRouter:
    """Router's interface and result layout (Router.py:33-251) for a complete graph whose direct edges are shortest.

    Router reports every target it has touched, settled or not, with its current cost (createRoutes reads
    cost_index, not settled). On a complete graph the first Dijkstra step, from the start node, touches every node with
    the direct-edge cost, and no later step finds a strictly cheaper path. That step is skipped only when the loop
    stops right after popping the start node (Router.py:278-288, :313-321): the start is itself the last target still
    to settle, or the time radius is negative. Then every target but the start keeps the infinite cost. Time radius
    and target limit do not drop touched targets; NetworkBasic's callers filter and trim afterwards."""

    def __init__(self, nw, start_node, destination_nodes=(), mode=None, time_radius=None, max_settled_targets=None,
                 forward_flag=True, ch_flag=False, customized_section_cost_function=None):
        self._fallback = None
        if customized_section_cost_function is not None or ch_flag:
            self._fallback = Router(nw, start_node, destination_nodes=destination_nodes, mode=mode,
                                    time_radius=time_radius, max_settled_targets=max_settled_targets,
                                    forward_flag=forward_flag, ch_flag=ch_flag,
                                    customized_section_cost_function=customized_section_cost_function)
            return
        self.nw = nw
        self.start = start_node
        self.destinations = list(dict.fromkeys(destination_nodes))
        self.mode = mode
        self.time_radius = time_radius
        self.max_settled_targets = max_settled_targets
        self.forward_flag = forward_flag

    def _edge(self, origin, destination):
        if origin == destination:
            return [origin], (0.0, 0.0, 0.0)
        travel_time, distance = self.nw.nodes[origin].get_travel_infos_to(destination)
        return [origin, destination], (travel_time, travel_time, distance)

    def compute(self, return_route=True):
        if self._fallback is not None:
            return self._fallback.compute(return_route=return_route)
        if self.mode == "bidirectional":
            solutions = []
            for end in self.destinations:
                route, cost = self._edge(self.start, end) if self.forward_flag else self._edge(end, self.start)
                if not return_route:
                    route = [self.start, end] if self.forward_flag else [end, self.start]
                solutions.append((route, cost))
            return solutions

        to_settle = len(self.destinations)
        if self.max_settled_targets is not None and self.max_settled_targets < to_settle:
            to_settle = self.max_settled_targets
        stopped_at_start = (self.start in self.destinations and to_settle == 1) or (
            self.time_radius is not None and 0 > self.time_radius)
        solutions = []
        for target in self.destinations:
            plain_route = [self.start, target] if self.forward_flag else [target, self.start]
            if target != self.start and stopped_at_start:
                solutions.append((plain_route, INFINITE))
                continue
            route, cost = self._edge(self.start, target) if self.forward_flag else self._edge(target, self.start)
            solutions.append((route if return_route else plain_route, cost))
        return solutions


def _with_direct_router(function):
    namespace = dict(function.__globals__)
    namespace["Router"] = DirectRouter
    rebound = types.FunctionType(function.__code__, namespace, function.__name__, function.__defaults__,
                                 function.__closure__)
    rebound.__doc__ = function.__doc__
    return rebound


class CompleteGraphDirectNetwork(NetworkBasic):
    """NetworkBasic whose node-to-node searches take the direct edge (see the module docstring)."""


for _name in ("return_travel_costs_1to1", "return_travel_costs_Xto1", "return_travel_costs_1toX",
              "return_best_route_1to1", "return_best_route_Xto1", "return_best_route_1toX"):
    setattr(CompleteGraphDirectNetwork, _name, _with_direct_router(getattr(NetworkBasic, _name)))
