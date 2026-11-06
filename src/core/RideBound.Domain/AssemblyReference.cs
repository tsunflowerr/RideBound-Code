using System.Reflection;

namespace RideBound.Domain;

public static class AssemblyReference
{
    public static Assembly Assembly => typeof(AssemblyReference).Assembly;
}
