using System.Reflection;

namespace RideBound.Algorithms;

public static class AssemblyReference
{
    public static Assembly Assembly => typeof(AssemblyReference).Assembly;
}
