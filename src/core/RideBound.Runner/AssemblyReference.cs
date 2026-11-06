using System.Reflection;

namespace RideBound.Runner;

public static class AssemblyReference
{
    public static Assembly Assembly => typeof(AssemblyReference).Assembly;
}
