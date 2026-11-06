using System.Runtime.CompilerServices;

// ADR-089: the model-identity tests compare the CP-SAT model built here with the archived builder byte for byte.
[assembly: InternalsVisibleTo("RideBound.Solvers.OrTools.Tests")]
