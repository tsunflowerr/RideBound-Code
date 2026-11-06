using System.Reflection;

namespace RideBound.Contracts;

public static class AssemblyReference
{
    public static Assembly Assembly => typeof(AssemblyReference).Assembly;
}
